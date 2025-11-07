using System.Collections.ObjectModel;

namespace ava_demo_new.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "离线";
        public string Type { get; set; } = "设备";
        public ObservableCollection<Device> Children { get; } = new ObservableCollection<Device>();
    }
}