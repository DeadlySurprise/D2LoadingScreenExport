using Newtonsoft.Json;
using SteamDatabase.ValvePak;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotaLoadScreenExport
{
    public static class LoadingScreenDB
    {
        public static List<LoadingScreenDBInfo> LoadFromJsonDB(string jsonDB)
        {
            return JsonConvert.DeserializeObject(jsonDB, typeof(List<LoadingScreenDBInfo>)) as List<LoadingScreenDBInfo>;
        }

        public static List<LoadingScreenBasicInfo> ToBasicInfo(List<LoadingScreenDBInfo> db)
        {
            return db.Select(dbInfo => new LoadingScreenBasicInfo(dbInfo)).ToList();
        }

        public static string GetJsonDB(List<LoadingScreenDBInfo> db)
        {
            return JsonConvert.SerializeObject(db, Formatting.Indented);
        }

        public static string GetBasicJson(List<LoadingScreenDBInfo> db)
        {
            return JsonConvert.SerializeObject(new { info = ToBasicInfo(db), dbDate = DateTime.UtcNow.ToLongDateString() }, Formatting.Indented);
        }

        public struct LoadingScreenDBInfo
        {
            public int ID;
            public string Name;
            public string ImageLink;
            public uint Crc32;
            public uint Size;
            public string FullPath;

            public bool Matches(PackageEntry entry)
            {
                return entry.CRC32 == Crc32 && entry.Length == Size && FullPath == entry.GetFullPath();
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + ID;
                    hash = hash * 23 + Name.GetHashCode();
                    hash = hash * 23 + ImageLink.GetHashCode();
                    hash = hash * 23 + (int)Crc32;
                    hash = hash * 23 + (int)Size;
                    hash = hash * 23 + FullPath.GetHashCode();
                    return hash;
                }
            }

            public override bool Equals(object obj)
            {
                if (obj.GetType() != typeof(LoadingScreenDBInfo))
                {
                    return false;
                }
                var other = (LoadingScreenDBInfo)obj;
                return
                    other.ID == ID && other.Name == Name &&
                    other.ImageLink == ImageLink && other.Crc32 == Crc32 &&
                    other.Size == Size && other.FullPath == FullPath;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public struct LoadingScreenBasicInfo
        {
            public string Name;
            public string ImageLink;

            public LoadingScreenBasicInfo(LoadingScreenDBInfo dbInfo)
            {
                Name = dbInfo.Name;
                ImageLink = dbInfo.ImageLink;
            }
        }
    }
}
