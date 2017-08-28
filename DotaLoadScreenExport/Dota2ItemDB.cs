namespace DotaLoadScreenExport
{
    using SteamDatabase.ValvePak;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class Dota2ItemDB
    {
        /// <summary>
        /// Gets a list of items matching the predicate from the given dota 2 apk package.
        /// </summary>
        /// <param name="apk">The dota 2 package apk to search.</param>
        /// <param name="predicate">A predicate for matching the item.</param>
        /// <returns>Returns a list containing the matched items.</returns>
        public static Task<List<DotaItem>> GetItems(Task<Package> apk, Func<DotaItem, bool> predicate)
        {
            return Task.Run(async () =>
            {
                var itemList = (await apk).Entries["txt"].Find(e => e.FileName == "items_game" && e.DirectoryName == "scripts/items");
                (await apk).ReadEntry(itemList, out var itemsGame);

                var lines = Encoding.ASCII.GetString(itemsGame).Split('\r', '\n');
                var objDepth = 0;
                var itemInfo = lines
                    .SkipWhile(l => l.Trim() != "\"items\"")
                    .Skip(1)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .TakeWhile(l =>
                    {
                        if (l.Trim() == "{")
                        {
                            objDepth++;
                        }
                        else if (l.Trim() == "}")
                        {
                            objDepth--;
                        }

                        return objDepth >= 0;
                    })
                    .ToList();

                objDepth = -1;

                return ReadItems(itemInfo, predicate);
            });
        }

        /// <summary>
        /// Reads all items matched by the matcher from the items array.
        /// </summary>
        /// <param name="itemInfo">Item array in Valve KeyValue format.</param>
        /// <param name="predicate">A predicate for matching the item.</param>
        /// <returns>Returns a list containing the matched items.</returns>
        private static List<DotaItem> ReadItems(List<string> itemInfo, Func<DotaItem, bool> predicate)
        {
            var objDepth = -1;
            var item = new DotaItem();
            var items = new List<DotaItem>();
            foreach (var line in itemInfo)
            {
                var tokens = GetTokens(line);
                if (tokens.Length == 1)
                {
                    if (tokens[0].Trim() == "{")
                    {
                        objDepth++;
                    }
                    else if (tokens[0].Trim() == "}")
                    {
                        objDepth--;
                        if (objDepth == 0)
                        {
                            if (predicate(item))
                            {
                                items.Add(item);
                                item = new DotaItem();
                            }
                        }
                        else if (objDepth < 0)
                        {
                            break;
                        }
                    }
                    else if (objDepth == 0)
                    {
                        if (tokens[0] == "default")
                        {
                            continue;
                        }

                        item.ID = int.Parse(tokens[0].Trim('"'));
                    }
                }
                else if (tokens.Length == 2)
                {
                    var val = tokens[1].Trim('"');
                    switch (tokens[0].Trim('"'))
                    {
                        case "name":
                            item.Name = val;
                            break;
                        case "prefab":
                            item.Type = val;
                            break;
                        case "asset":
                            item.Path = val;
                            break;
                    }
                }
                else
                {
                    throw new InvalidDataException();
                }
            }

            return items;
        }

        private static string[] GetTokens(string str)
        {
            var strPtr = 0;
            var inToken = false;
            var isEscaped = false;
            var token = "";
            var tokens = new List<string>();
            while (strPtr < str.Length)
            {
                var c = str[strPtr++];
                if (c == '"')
                {
                    if (isEscaped)
                    {
                        token += '"';
                    }
                    else
                    {
                        if (inToken)
                        {
                            tokens.Add(token);
                            token = "";
                        }

                        inToken = !inToken;
                    }
                }
                else if (c == '\\')
                {
                    if (isEscaped)
                    {
                        token += '\\';
                    }

                    isEscaped = !isEscaped;
                }
                else if (inToken)
                {
                    token += c;
                }
            }

            if (tokens.Count == 0)
            {
                return new string[] { str };
            }

            return tokens.ToArray();
        }

        public struct DotaItem
        {
            public int ID;
            public string Name;
            public string Type;
            public string Path;

            public override string ToString()
            {
                return $"{Name}|{Path}";
            }
        }

    }
}
