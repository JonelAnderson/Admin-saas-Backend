using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Customers.Request;
using DS.Platform.Application.DTOs.Customers.Response;
using DS.Platform.Application.Features.Customers;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Customers
{
    /// <summary>Mapeo explícito de clientes (sin AutoMapper). Estado y auditoría los ponen el handler y el contexto.</summary>
    public static class CustomerMappings
    {
        public static readonly Expression<Func<Customer, CustomerResponseDto>> ToResponse = c => new CustomerResponseDto
        {
            Id = c.Id,
            Ruc = c.Ruc,
            BusinessName = c.BusinessName,
            TradeName = c.TradeName,
            Address = c.Address,
            ContactName = c.ContactName,
            ContactEmail = c.ContactEmail,
            ContactPhone = c.ContactPhone,
            Notes = c.Notes,
            IsActive = c.IsActive,
            OpenSubscriptions = c.Subscriptions.Count(s => !s.IsDeleted && s.Status != SubscriptionStatus.Cancelled)
        };

        public static readonly Expression<Func<Customer, CustomerLookupResponseDto>> ToLookup = c => new CustomerLookupResponseDto
        {
            Id = c.Id,
            Ruc = c.Ruc,
            BusinessName = c.BusinessName,
            TradeName = c.TradeName
        };

        public static Customer ToEntity(this CustomerRequestDto dto)
        {
            var customer = new Customer { Ruc = RucRules.Normalize(dto.Ruc), BusinessName = dto.BusinessName.Trim() };
            CopyEditable(dto, customer);
            return customer;
        }

        /// <summary>Copia los datos editables del formulario al cliente (no toca id, estado ni auditoría).</summary>
        public static void ApplyTo(this CustomerRequestDto dto, Customer customer)
        {
            customer.Ruc = RucRules.Normalize(dto.Ruc);
            customer.BusinessName = dto.BusinessName.Trim();
            CopyEditable(dto, customer);
        }

        private static void CopyEditable(CustomerRequestDto dto, Customer customer)
        {
            customer.TradeName = Clean(dto.TradeName);
            customer.Address = Clean(dto.Address);
            customer.ContactName = Clean(dto.ContactName);
            customer.ContactEmail = Clean(dto.ContactEmail)?.ToLowerInvariant();
            customer.ContactPhone = Clean(dto.ContactPhone);
            customer.Notes = Clean(dto.Notes);
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
