using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ava_demo_new.Models;

namespace ava_demo_new.Services
{
    public class HttpDeviceService : IDeviceService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.yourcompany.com";

        public HttpDeviceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ObservableCollection<Device>> GetDevicesAsync()
        {
            // 模拟网络延迟
            await Task.Delay(1000);
            
            // 直接返回模拟数据
            return new ObservableCollection<Device>()
            {
                new Device
                {
                    Id = 1,
                    Name = "设备组1",
                    Type = "设备组",
                    Status = "在线",
                    Children =
                    {
                        new Device { Id = 101, Name = "设备1-1", Type = "设备", Status = "在线" },
                        new Device { Id = 102, Name = "设备1-2", Type = "设备", Status = "离线" },
                        new Device { Id = 103, Name = "设备1-3", Type = "设备", Status = "在线" }
                    }
                },
                new Device
                {
                    Id = 2,
                    Name = "设备组2", 
                    Type = "设备组",
                    Status = "在线",
                    Children =
                    {
                        new Device { Id = 201, Name = "设备2-1", Type = "设备", Status = "在线" },
                        new Device { Id = 202, Name = "设备2-2", Type = "设备", Status = "在线" },
                        new Device 
                        { 
                            Id = 203, 
                            Name = "子设备组2-3", 
                            Type = "子设备组",
                            Status = "维护中",
                            Children =
                            {
                                new Device { Id = 2031, Name = "设备2-3-1", Type = "设备", Status = "在线" },
                                new Device { Id = 2032, Name = "设备2-3-2", Type = "设备", Status = "离线" }
                            }
                        }
                    }
                },
                new Device
                {
                    Id = 3,
                    Name = "设备组3",
                    Type = "设备组", 
                    Status = "离线",
                    Children =
                    {
                        new Device { Id = 301, Name = "设备3-1", Type = "设备", Status = "维护中" },
                        new Device { Id = 302, Name = "设备3-2", Type = "设备", Status = "离线" }
                    }
                }
            };
            
            // 如果以后要连接API，取消注释下面的代码，删除上面的模拟数据
            /*
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/devices");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var devices = JsonSerializer.Deserialize<ObservableCollection<Device>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return devices ?? new ObservableCollection<Device>();
            }
            catch (Exception ex)
            {
                throw new Exception($"获取设备数据失败: {ex.Message}", ex);
            }
            */
        }

        public async Task<bool> UpdateDeviceStatusAsync(int deviceId, string status)
        {
            // 模拟网络延迟
            await Task.Delay(500);
            
            // 直接返回成功（模拟）
            Console.WriteLine($"模拟更新设备 {deviceId} 状态为: {status}");
            return true;
            
            // 如果以后要连接真实 API，取消注释下面的代码
            /*
            try
            {
                var requestData = new { status };
                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"{BaseUrl}/api/devices/{deviceId}/status", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新设备状态失败: {ex.Message}");
                return false;
            }
            */
        }

        public async Task<Device> GetDeviceByIdAsync(int id)
        {
            // 模拟网络延迟
            await Task.Delay(300);
            
            // 直接返回模拟数据
            return new Device { Id = id, Name = $"设备{id}", Status = "在线" };
            
            // 如果以后要连接真实 API，取消注释下面的代码
            /*
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/devices/{id}");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var device = JsonSerializer.Deserialize<Device>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return device ?? new Device();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取设备详情失败: {ex.Message}");
                return new Device();
            }
            */
        }
    }
}