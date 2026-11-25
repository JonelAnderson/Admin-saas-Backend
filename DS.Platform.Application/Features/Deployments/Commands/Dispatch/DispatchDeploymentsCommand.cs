using DS.Platform.Application.Features.Deployments.Common;
using DS.Platform.Application.Features.RemoteActions.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Coolify;
using DS.Platform.Application.Interfaces.Services.Security;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Deployments.Commands.Dispatch
{
    /// <summary>
    /// Ejecuta en Coolify los despliegues que toca (fase C5). Lo envía el job <c>DeploymentsDispatch</c> cada minuto. Igual que el
    /// despachador de acciones remotas: toma un lote marcándolo En curso en un guardado propio (si otro despachador lo tomó antes,
    /// el guardado choca por <c>xmin</c> y este no hace nada) y cada despliegue guarda su resultado por separado.
    /// Un <c>Create</c> avanza por pasos (crear el servicio → cargar variables → arrancar) y guarda cada uno al terminar: un
    /// reintento continúa donde quedó, nunca crea un segundo servicio ni toca las variables de un stack que ya arrancó.
    /// Devuelve cuántos terminaron.
    /// </summary>
    public class DispatchDeploymentsCommand : IRequest<Result<int>>
    {
        /// <summary>Máximo de despliegues por pasada.</summary>
        public int Batch { get; set; } = 5;
    }

    internal class DispatchDeploymentsCommandHandler(
        IUnitOfWork<int> unitOfWork,
        ICoolifyClient coolify,
        IProvisioningSettings settings,
        ISecretProtector protector,
        IDateTimeService dateTime) : IRequestHandler<DispatchDeploymentsCommand, Result<int>>
    {
        public const string InstanceGoneMessage = "La instancia ya no existe en la consola.";
        public const string SecretsMissingMessage = "Faltan los secretos del stack (se borraron o no se pueden leer): cancele el despliegue y móntelo de nuevo.";
        public const string NoUrlMessage = "La instancia no tiene una dirección válida: cancele el despliegue y móntelo de nuevo.";
        public const string NoServiceMessage = "La instancia no tiene servicio en Coolify.";

        private sealed record Failure(string Error, bool Retryable);

        public async Task<Result<int>> Handle(DispatchDeploymentsCommand request, CancellationToken cancellationToken)
        {
            var now = dateTime.NowUtc;
            var due = await unitOfWork.InstanceDeployments.GetDueAsync(now, Math.Clamp(request.Batch, 1, 20), cancellationToken);
            if (due.Count == 0)
            {
                return await Result<int>.SuccessAsync(0, "No hay despliegues pendientes.");
            }

            foreach (var deployment in due)
            {
                deployment.Status = RemoteActionStatus.Sending;
                deployment.LockedUntilUtc = now + RemoteActionRetryPolicy.Lease;
            }
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                unitOfWork.ClearChanges();
                return await Result<int>.SuccessAsync(0, "Otro despachador tomó los despliegues.");
            }

            var done = 0;
            foreach (var deployment in due)
            {
                if (await ProcessAsync(deployment, cancellationToken))
                {
                    done++;
                }
            }
            return await Result<int>.SuccessAsync(done, $"{done} despliegues terminados.");
        }

        private async Task<bool> ProcessAsync(InstanceDeployment deployment, CancellationToken cancellationToken)
        {
            var finished = false;
            try
            {
                var instance = await unitOfWork.ErpInstances.GetByIdAsync(deployment.InstanceId);
                if (instance is null || instance.IsDeleted)
                {
                    Finish(deployment, RemoteActionStatus.Abandoned, InstanceGoneMessage);
                }
                else
                {
                    var failure = deployment.Type == DeploymentType.Create
                        ? await RunCreateAsync(deployment, instance, cancellationToken)
                        : await RunUpdateAsync(deployment, instance, cancellationToken);

                    deployment.Attempts++;
                    if (failure is null)
                    {
                        Finish(deployment, RemoteActionStatus.Done, null);
                        deployment.DoneOnUtc = dateTime.NowUtc;
                        deployment.SecretsProtected = null;
                        instance.DeployedVersion = deployment.Version;
                        await unitOfWork.ErpInstances.UpdateAsync(instance);
                        finished = true;
                    }
                    else
                    {
                        deployment.LastError = failure.Error.Length > 1000 ? failure.Error[..1000] : failure.Error;
                        deployment.LockedUntilUtc = null;
                        if (!failure.Retryable || RemoteActionRetryPolicy.IsExhausted(deployment.Attempts))
                        {
                            deployment.Status = RemoteActionStatus.Abandoned;
                        }
                        else
                        {
                            deployment.Status = RemoteActionStatus.Failed;
                            deployment.NextAttemptOnUtc = dateTime.NowUtc + RemoteActionRetryPolicy.DelayAfter(deployment.Attempts);
                        }
                    }
                }

                await unitOfWork.InstanceDeployments.UpdateAsync(deployment);
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Mientras se ejecutaba, alguien lo cambió (p. ej. lo cancelaron): vale lo que hay en la base.
                unitOfWork.ClearChanges();
                return false;
            }
            return finished;
        }

        private async Task<Failure?> RunCreateAsync(InstanceDeployment deployment, ErpInstance instance, CancellationToken cancellationToken)
        {
            if (!settings.IsConfigured)
            {
                return new Failure(ProvisioningRules.NotConfiguredMessage, true);
            }
            var secrets = string.IsNullOrEmpty(deployment.SecretsProtected)
                ? null
                : ProvisioningSecretsJson.Deserialize(protector.Unprotect(deployment.SecretsProtected));
            if (secrets is null)
            {
                return new Failure(SecretsMissingMessage, false);
            }
            var host = ProvisioningRules.HostOf(instance.Url);
            if (host is null)
            {
                return new Failure(NoUrlMessage, false);
            }

            if (deployment.Step < DeploymentStep.ServiceCreated)
            {
                var spec = new CoolifyServiceSpec(
                    ProvisioningRules.ServiceName(host, instance.Id), $"ERP MultiEmpresa - instancia {instance.Id}",
                    InstanceStack.ComposeYaml, InstanceStack.PublicService, instance.Url!, InstanceStack.PublicPort);
                var created = await coolify.CreateServiceAsync(spec, cancellationToken);
                if (!created.Success)
                {
                    return new Failure(created.Error ?? $"HTTP {created.StatusCode}", created.Retryable);
                }
                // El servicio ya existe en Coolify: se guarda ahora mismo, antes de seguir (un reintento no lo crea otra vez).
                instance.CoolifyServiceUuid = created.Data;
                deployment.Step = DeploymentStep.ServiceCreated;
                await unitOfWork.ErpInstances.UpdateAsync(instance);
                await unitOfWork.InstanceDeployments.UpdateAsync(deployment);
                await unitOfWork.Commit(cancellationToken);
            }

            var uuid = instance.CoolifyServiceUuid!;
            if (deployment.Step < DeploymentStep.Configured)
            {
                foreach (var (key, value) in InstanceStack.Environment(secrets, settings, host, deployment.Version))
                {
                    var set = await coolify.SetEnvAsync(uuid, key, value, cancellationToken);
                    if (!set.Success)
                    {
                        // Sin el valor: puede ser un secreto.
                        return new Failure($"No se pudo cargar la variable {key}: {set.Error ?? $"HTTP {set.StatusCode}"}", set.Retryable);
                    }
                }
                deployment.Step = DeploymentStep.Configured;
                await unitOfWork.InstanceDeployments.UpdateAsync(deployment);
                await unitOfWork.Commit(cancellationToken);
            }

            if (deployment.Step < DeploymentStep.Started)
            {
                var started = await coolify.StartServiceAsync(uuid, cancellationToken);
                if (!started.Success)
                {
                    return new Failure(started.Error ?? $"HTTP {started.StatusCode}", started.Retryable);
                }
                deployment.Step = DeploymentStep.Started;
            }
            return null;
        }

        private async Task<Failure?> RunUpdateAsync(InstanceDeployment deployment, ErpInstance instance, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(instance.CoolifyServiceUuid))
            {
                return new Failure(NoServiceMessage, false);
            }
            var uuid = instance.CoolifyServiceUuid;

            var set = await coolify.SetEnvAsync(uuid, "ERP_VERSION", deployment.Version, cancellationToken);
            if (!set.Success)
            {
                return new Failure($"No se pudo cambiar la versión: {set.Error ?? $"HTTP {set.StatusCode}"}", set.Retryable);
            }
            var restarted = await coolify.RestartServiceAsync(uuid, pullLatest: true, cancellationToken);
            return restarted.Success ? null : new Failure(restarted.Error ?? $"HTTP {restarted.StatusCode}", restarted.Retryable);
        }

        private static void Finish(InstanceDeployment deployment, RemoteActionStatus status, string? error)
        {
            deployment.Status = status;
            deployment.LockedUntilUtc = null;
            deployment.LastError = error;
        }
    }
}
