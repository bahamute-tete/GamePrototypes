using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.IO;

public static class RLEParser
{
    /// <summary>
    /// 从文件路径加载并解析 RLE 文件
    /// </summary>
    public static Vector2Int[] LoadAndParse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"RLE file not found at: {filePath}");
            return new Vector2Int[0];
        }

        string content = File.ReadAllText(filePath);
        return Parse(content);
    }

    /// <summary>
    /// 解析 Run Length Encoded (RLE) 格式的生命游戏图案
    /// </summary>
    public static Vector2Int[] Parse(string rle)
    {
        if (string.IsNullOrEmpty(rle)) return new Vector2Int[0];

        List<Vector2Int> liveCells = new List<Vector2Int>();
        
        // 预处理：移除头部信息和注释，合并数据行
        string[] lines = rle.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        StringBuilder sb = new StringBuilder();
        
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed.StartsWith("#")) continue; // 注释
            if (trimmed.StartsWith("x =") || trimmed.StartsWith("x=")) continue; // 头部信息
            
            sb.Append(trimmed);
        }
        
        string data = sb.ToString();
        
        int x = 0;
        int y = 0;
        int count = 0;

        for (int i = 0; i < data.Length; i++)
        {
            char c = data[i];

            if (char.IsDigit(c))
            {
                count = count * 10 + (c - '0');
            }
            else
            {
                if (count == 0) count = 1;

                if (c == 'b') // Dead cell
                {
                    x += count;
                }
                else if (c == 'o') // Live cell
                {
                    for (int k = 0; k < count; k++)
                    {
                        // RLE 中 y 通常向下增长，这里直接映射
                        liveCells.Add(new Vector2Int(x + k, y));
                    }
                    x += count;
                }
                else if (c == '$') // End of line
                {
                    y += count;
                    x = 0;
                }
                else if (c == '!') // End of pattern
                {
                    break;
                }
                
                count = 0;
            }
        }

        return liveCells.ToArray();
    }


}
