namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class MfaStatusResponse
    {
        public bool Enabled { get; set; }
        public int RecoveryCodesLeft { get; set; }
    }
}
