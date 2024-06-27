using System.Collections.Generic;

namespace PufferSoftware.Aurora.Drive
{
    public class DriveItem
    {
        public string Name { get; }
        public string Id { get; }
        public string MimeType { get; }
        public long? Size { get; }
        public string Path { get; }

        public List<DriveItem> Children { get; }

        public DriveItem(string name, string id, string mimeType, long? size = null, string path = "")
        {
            Name = name;
            Id = id;
            MimeType = mimeType;
            Size = size;
            Path = path;
            Children = new List<DriveItem>();
        }

        public void AddChild(DriveItem child)
        {
            Children.Add(child);
        }
    }
}