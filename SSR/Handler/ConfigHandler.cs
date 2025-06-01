using Newtonsoft.Json;
using web3script.Mode;
using System.IO;

namespace web3script.Handler
{
    internal class ConfigHandler
    {
        private static string configRes = Global.ConfigFileName;
        private static readonly object objLock = new();

        public static int LoadConfig(ref Config config)
        {
           
            if (!File.Exists(Utils.GetConfigPath(configRes)))
            {
                config = new Config();
                ToJsonFile(config);
                return 0;
            }

            try
            {
               
                string configContent = File.ReadAllText(Utils.GetConfigPath(configRes));
                config = JsonConvert.DeserializeObject<Config>(configContent) ?? new Config();

                return 0;
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Loading config failed: " + ex.Message);
                return -1;
            }
        }

        public static int SaveConfig(ref Config config)
        {
            try
            {
                ToJsonFile(config);
                return 0;
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Saving config failed: " + ex.Message);
                return -1;
            }
        }

        private static void ToJsonFile(Config config)
        {
            try
            {
                // 确保配置目录存在
                var configPath = Utils.GetConfigPath();
                if (!Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                }

                // 保存为JSON文件
                string jsonStr = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(Utils.GetConfigPath(configRes), jsonStr);
            }
            catch (Exception ex)
            {
                Utils.SaveLog("ToJsonFile failed: " + ex.Message);
            }
        }

        public static List<ProfileItem> GetProfiles()
        {
            Config config = new Config();
            LoadConfig(ref config);
            return config.profileItems ?? new List<ProfileItem>();
        }

        public static int AddCustomServer(ref Config config, ProfileItem profileItem, bool blDelete)
        {
            var fileName = profileItem.address;
            if (!File.Exists(fileName))
            {
                Utils.SaveLog($"AddCustomServer: 文件不存在 {fileName}");
                return -1;
            }

            Utils.SaveLog($"AddCustomServer: 处理文件 {fileName}, 扩展名 {Path.GetExtension(fileName)}");

            // 生成新的文件名，确保唯一性
            var ext = Path.GetExtension(fileName);
            string newFileName = $"{DateTime.Now.ToString("yyyyMMddHHmmss")}_{Utils.GetGUID()}{ext}";
            string configPath = Utils.GetConfigPath();
            string fullDestPath = Path.Combine(configPath, newFileName);

            Utils.SaveLog($"AddCustomServer: 配置目录路径: {configPath}");
            Utils.SaveLog($"AddCustomServer: 新文件完整路径: {fullDestPath}");

            try
            {
                // 确保配置目录存在
                if (!Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                    Utils.SaveLog($"AddCustomServer: 创建配置目录: {configPath}");
                }

              
                Utils.SaveLog($"AddCustomServer: 复制文件 {fileName} 到 {fullDestPath}");
                File.Copy(fileName, fullDestPath, true);  

                if (blDelete)
                {
                    File.Delete(fileName);
                    Utils.SaveLog($"AddCustomServer: 删除原文件 {fileName}");
                }

              
                profileItem.address = fullDestPath;  
                profileItem.configType = "converted_yaml";  

                if (string.IsNullOrEmpty(profileItem.remarks))
                {
                    profileItem.remarks = $"import custom@{DateTime.Now.ToShortDateString()}";
                }

                Utils.SaveLog($"AddCustomServer: 最终配置类型: {profileItem.configType}, 文件路径: {profileItem.address}");

               
                int result = AddServerCommon(ref config, profileItem);
                Utils.SaveLog($"AddCustomServer: AddServerCommon结果 {result}");
                return result;
            }
            catch (Exception ex)
            {
                Utils.SaveLog($"AddCustomServer failed: {ex.Message}, StackTrace: {ex.StackTrace}");
                return -1;
            }
        }

        public static int EditCustomServer(ref Config config, ProfileItem profileItem)
        {
            Utils.SaveLog($"EditCustomServer: 开始编辑服务器 ID={profileItem.indexId}, 类型={profileItem.configType}");
           
            if (config.profileItems == null)
            {
                config.profileItems = new List<ProfileItem>();
                Utils.SaveLog("EditCustomServer: 配置项列表为空，创建新列表");
            }

            var index = config.profileItems.FindIndex(p => p.indexId == profileItem.indexId);
            Utils.SaveLog($"EditCustomServer: 查找服务器索引结果 {index}");

            if (index >= 0)
            {
                config.profileItems[index] = profileItem;
                SaveConfig(ref config);
                Utils.SaveLog($"EditCustomServer: 更新服务器成功 {profileItem.remarks}");
                return 0;
            }

            Utils.SaveLog($"EditCustomServer: 未找到要编辑的服务器 ID={profileItem.indexId}");
            return -1;
        }

        public static int AddServerCommon(ref Config config, ProfileItem profileItem)
        {
            if (config.profileItems == null)
            {
                config.profileItems = new List<ProfileItem>();
            }

            if (string.IsNullOrEmpty(profileItem.indexId))
            {
                profileItem.indexId = Utils.GetGUID();
            }

            var index = config.profileItems.FindIndex(p => p.indexId == profileItem.indexId);
            if (index >= 0)
            {
                config.profileItems[index] = profileItem;
            }
            else
            {
                config.profileItems.Add(profileItem);
            }

            SaveConfig(ref config);
            return 0;
        }

        public static int SetDefaultServerIndex(ref Config config, string indexId)
        {
            if (string.IsNullOrEmpty(indexId))
            {
                return -1;
            }

            config.indexId = indexId;
            SaveConfig(ref config);
            return 0;
        }

        public static ProfileItem? GetDefaultServer(ref Config config)
        {
            if (string.IsNullOrEmpty(config.indexId) || config.profileItems == null || config.profileItems.Count == 0)
            {
                return null;
            }

            string targetId = config.indexId;
            return config.profileItems.FirstOrDefault(p => p.indexId == targetId);
        }
    }
}