using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ava_demo_new.Models;

namespace ava_demo_new.Services
{
    // 定义设备服务接口
    public interface IDeviceService
    {
        // 模拟从服务器获取设备数据
        Task<ObservableCollection<Device>> GetDevicesAsync();
        
        // 可以根据需要添加其他接口方法
        Task<bool> UpdateDeviceStatusAsync(int deviceId, string status);
        Task<Device> GetDeviceByIdAsync(int id);
    }
}