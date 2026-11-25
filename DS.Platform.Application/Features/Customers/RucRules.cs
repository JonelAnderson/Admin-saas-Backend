namespace DS.Platform.Application.Features.Customers
{
    /// <summary>
    /// RUC peruano (cálculo puro): 11 dígitos, prefijo 10 / 15 / 16 / 17 / 20 y dígito verificador módulo 11 (pesos
    /// 5,4,3,2,7,6,5,4,3,2). Evita registrar clientes con un RUC mal escrito (y luego facturarles con él).
    /// </summary>
    public static class RucRules
    {
        public const string InvalidMessage = "El RUC debe tener 11 dígitos válidos (revise el número y su dígito verificador).";

        private static readonly int[] Weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        private static readonly string[] Prefixes = ["10", "15", "16", "17", "20"];

        public static string Normalize(string? ruc) => (ruc ?? string.Empty).Trim();

        public static bool IsValid(string? ruc)
        {
            var value = Normalize(ruc);
            if (value.Length != 11 || !value.All(char.IsAsciiDigit) || !Prefixes.Contains(value[..2]))
            {
                return false;
            }

            var sum = 0;
            for (var i = 0; i < 10; i++)
            {
                sum += (value[i] - '0') * Weights[i];
            }
            var check = 11 - sum % 11;
            check = check switch { 10 => 0, 11 => 1, _ => check };
            return check == value[10] - '0';
        }
    }
}
