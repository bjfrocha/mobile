using System;
using SQLite.Net.Attributes;

namespace Toggl.Phoebe._Data.Models
{
    [Table ("ClientModel")]
    public class ClientData : CommonData
    {
        public ClientData ()
        {
        }

        public ClientData (ClientData other) : base (other)
        {
            Name = other.Name;
            WorkspaceId = other.WorkspaceId;
        }

        public string Name { get; set; }

        public Guid WorkspaceId { get; set; }
    }
}
