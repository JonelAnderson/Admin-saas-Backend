namespace DS.Platform.Shared.Static
{
    public class ExcelColumnNames
    {
        public static List<TableColumn> GetColumns(IEnumerable<(string ColumnName, string PropertyName)> columnsProperties)
        {
            var columns = new List<TableColumn>();

            foreach (var (ColumnName, PropertyName) in columnsProperties)
            {
                var column = new TableColumn()
                {
                    Label = ColumnName,
                    PropertyName = PropertyName
                };

                columns.Add(column);
            }

            return columns;
        }

        #region ColumnsProspects
        public static List<(string ColumnName, string PropertyName)> GetColumnsProspects()
        {
            var columnsProperties = new List<(string ColumnName, string PropertyName)>
            {
                ("N° Prospecto", "ProspectNumber"),
                ("Ciudad", "City"),
                ("Tienda", "Store"),
                ("Vendedor", "Seller"),
                ("Tipo de Documento", "DocumentType"),
                ("Número de Documento", "DocumentNumber"),
                ("Nombres", "Name"),
                ("Apellidos", "FullLastName"),
                ("Número de Teléfono", "PhoneNumber"),
                ("Correo Electrónico", "Email"),
                ("Dependientes", "Dependences"),
                ("Tipo Vivienda", "TypeHome"),
                ("Tiempo residencia (meses)", "TimeResidenceMonth"),
                ("Tipo Cliente", "TypeCustomer"),
                ("Detalle Cliente", "DetailCustomer"),
                ("Perfil Cliente", "ProfileCustomer"),
                ("Valor por Tipo Vivienda", "ProfileHouseValue"),
                ("Tasa Mesual", "MonthlyRate"),
                ("Bienes a Financiar", "FinancialAssets"),
                ("Marca", "Brand"),
                ("Valor de Bienes", "AssetValue"),
                ("Cuota Inicial", "InitialQuota"),
                ("Valor a Financiar", "ValueFinancial"),
                ("Plazo en meses", "TermMonth"),
                ("Frecuencia", "Frecuence"),
                ("Valor de Cuota", "QuotaValue"),
                ("Fecha de Registro", "RegisterDate"),
                ("Estado del Prospecto", "StateProspectName"),
                ("Estado Solicitud", "GeneralStateName"),
                ("Comentarios", "Comment")
            };

            return columnsProperties;
        }


        public static List<(string ColumnName, string PropertyName)> GetColumnsReportsProspects()
        {
            var columnsProperties = new List<(string ColumnName, string PropertyName)>
            {
                ("N° Prospecto", "ProspectNumber"),
                ("Ciudad", "City"),
                ("Tienda", "Store"),
                ("Vendedor", "Seller"),
                ("Tipo de Documento", "DocumentType"),
                ("Número de Documento", "DocumentNumber"),
                ("Nombres", "Name"),
                ("Apellidos", "FullLastName"),
                ("Número de Teléfono", "PhoneNumber"),
                ("Correo Electrónico", "Email"),
                ("Dependientes", "Dependences"),
                ("Tipo Vivienda", "TypeHome"),
                ("Tiempo residencia (meses)", "TimeResidenceMonth"),
                ("Tipo Cliente", "TypeCustomer"),
                ("Detalle Cliente", "DetailCustomer"),
                ("Perfil Cliente", "ProfileCustomer"),
                ("Valor por Tipo Vivienda", "ProfileHouseValue"),
                ("Tasa Mesual", "MonthlyRate"),
                ("Bienes a Financiar", "FinancialAssets"),
                ("Marca", "Brand"),
                ("Valor de Bienes", "AssetValue"),
                ("Cuota Inicial", "InitialQuota"),
                ("Valor a Financiar", "ValueFinancial"),
                ("Plazo en meses", "TermMonth"),
                ("Frecuencia", "Frecuence"),
                ("Valor de Cuota", "QuotaValue"),
                ("Fecha de Registro", "RegisterDate"),
                ("Estado del Prospecto", "StateProspectName"),
                ("Estado Solicitud", "GeneralStateName"),
                ("Fecha Aprobación Solicitud", "RegisterDateStateAproved"),
                ("Usuario Aprobó Solicitud", "UserNameAproved"),
                ("Comentario Solicitud", "SolicitudeComment"),
                ("Comentario Prospecto", "Comment"),
                ("Observaciones", "Observation")

            };

            return columnsProperties;
        }
        #endregion


        #region ColumnsSolicitudes
        public static List<(string ColumnName, string PropertyName)> GetColumnsSolicitudes()
        {
            var columnsProperties = new List<(string ColumnName, string PropertyName)>
            {
                ("N° Solicitud", "SolicitudeNumber"),
                ("Tipo Documento ", "DocumentType"),
                ("N° Documento ", "DocumentNumber"),
                ("Nombres ", "Name"),
                ("Apellidos", "FullLastName"),
                ("Número de Teléfono", "PhoneNumber"),
                ("Estado Civil", "CivilState"),
                ("Fecha Cumpleaños", "BirthdayDateDescription"),
                ("Dependiente", "Dependences"),
                ("Tipo de Vivienda", "TypeHome"),
                ("Tiempo Residencia (Meses)", "TimeResidenceMonth"),
                ("ESTADO", "SolicitudeStateName"),
                ("Comentario", "Comment")
               
            };

            return columnsProperties;
        }


        #endregion
    }
}