using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    public class ColorManip
    {
        public static Color ColorHash(string inputString)  // https://github.com/RolandR/ColorHash/blob/master/colorhash.js
        {
            var sum = 0;
            foreach (char c in inputString)
            {
                sum += c;
            }

            float r = float.Parse("0." + Math.Sin(sum + 1).ToString().Substring(6));
            float g = float.Parse("0." + Math.Sin(sum + 2).ToString().Substring(6));
            float b = float.Parse("0." + Math.Sin(sum + 3).ToString().Substring(6));

            return new Color(r, g, b);
        }
    }
}
