using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SilaGeneratorWpf.Services
{
    /// <summary>
    /// 用户偏好设置服务
    /// </summary>
    public class UserPreferencesService
    {
        private readonly ILogger _logger;
        private readonly string _preferencesPath;
        private UserPreferences? _preferences;

        public UserPreferencesService()
        {
            _logger = LoggerService.GetLogger<UserPreferencesService>();
            _preferencesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SilaGenerator",
                "preferences.json");

            LoadPreferences();
        }

        /// <summary>
        /// 获取当前偏好设置
        /// </summary>
        public UserPreferences Preferences => _preferences ?? new UserPreferences();

        /// <summary>
        /// 加载偏好设置
        /// </summary>
        private void LoadPreferences()
        {
            try
            {
                if (File.Exists(_preferencesPath))
                {
                    var json = File.ReadAllText(_preferencesPath);
                    _preferences = JsonSerializer.Deserialize<UserPreferences>(json);
                    _logger.LogInformation("用户偏好设置加载成功");
                }
                else
                {
                    _preferences = new UserPreferences();
                    _logger.LogInformation("使用默认偏好设置");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载用户偏好设置失败");
                _preferences = new UserPreferences();
            }
        }

        /// <summary>
        /// 保存偏好设置
        /// </summary>
        public void SavePreferences()
        {
            try
            {
                var directory = Path.GetDirectoryName(_preferencesPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                var json = JsonSerializer.Serialize(_preferences, options);
                File.WriteAllText(_preferencesPath, json);
                
                _logger.LogInformation("用户偏好设置保存成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存用户偏好设置失败");
            }
        }

        /// <summary>
        /// 更新最后使用的设备信息
        /// </summary>
        public void UpdateLastDeviceInfo(string brand, string model, string deviceType, string developer)
        {
            if (_preferences == null) return;

            _preferences.LastBrand = brand;
            _preferences.LastModel = model;
            _preferences.LastDeviceType = deviceType;
            _preferences.LastDeveloper = developer;
            _preferences.LastUpdateTime = DateTime.Now;

            SavePreferences();
        }

        /// <summary>
        /// 更新窗口状态
        /// </summary>
        public void UpdateWindowState(double left, double top, double width, double height, bool isMaximized)
        {
            if (_preferences == null) return;

            _preferences.WindowLeft = left;
            _preferences.WindowTop = top;
            _preferences.WindowWidth = width;
            _preferences.WindowHeight = height;
            _preferences.WindowMaximized = isMaximized;

            SavePreferences();
        }

        /// <summary>
        /// 更新侧边栏状态
        /// </summary>
        public void UpdateSidebarState(bool isVisible, double width)
        {
            if (_preferences == null) return;

            _preferences.SidebarVisible = isVisible;
            _preferences.SidebarWidth = width;

            SavePreferences();
        }
    }

    /// <summary>
    /// 用户偏好设置
    /// </summary>
    public class UserPreferences
    {
        // 窗口状态
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 700;
        public bool WindowMaximized { get; set; } = false;

        // 侧边栏状态
        public bool SidebarVisible { get; set; } = true;
        public double SidebarWidth { get; set; } = 300;

        // 最后使用的设备信息
        public string LastBrand { get; set; } = "";
        public string LastModel { get; set; } = "";
        public string LastDeviceType { get; set; } = "";
        public string LastDeveloper { get; set; } = "Bioyond";

        // 其他偏好
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
    }
}

