using System.ComponentModel;

namespace DS.Platform.Domain.Enums
{
    public enum UploadType : byte
    {
        [Description(@"Images\Prospects")]
        Prospect,

        [Description(@"Images\ProfilePictures")]
        ProfilePicture,

        [Description(@"Documents")]
        Document
    }
}