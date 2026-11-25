using System.ComponentModel;

namespace DS.Platform.Domain.Enums
{
    public enum Gender
    {
        [Description("MASCULINO")]
        Male,

        [Description("FEMENINO")]
        Female,

        [Description("OTRO")]
        Other

    }
}