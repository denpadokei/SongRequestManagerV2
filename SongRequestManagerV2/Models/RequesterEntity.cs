using ChatCore.Interfaces;
using ChatCore.Utilities;
using System;

namespace SongRequestManagerV2.Models
{
    public class RequesterEntity : IChatUser
    {
        public string Id { get; set; } = "";

        public string UserName { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string Color { get; set; } = "";

        public bool IsBroadcaster { get; set; }

        public bool IsModerator { get; set; }

        public IChatBadge[] Badges { get; set; } = Array.Empty<IChatBadge>();

        public JSONObject ToJson()
        {
            var obj = new JSONObject();
            obj["Id"] = this.Id;
            obj["UserName"] = this.UserName;
            obj["DisplayName"] = this.DisplayName;
            obj["Color"] = this.Color;
            obj["IsBroadcaster"] = new JSONBool(this.IsBroadcaster);
            obj["IsModerator"] = new JSONBool(this.IsModerator);
            var array = new JSONArray();
            foreach (var badge in this.Badges) {
                array.Add(badge.ToJson());
            }
            obj["Badges"] = array;
            return obj;
        }

        public RequesterEntity()
        {
            this.Id = "unknow";
            this.UserName = "unknow";
            this.DisplayName = "unknown";
            this.Color = "#FFFFFF";
            this.Badges = new IChatBadge[0];
        }
    }
}
