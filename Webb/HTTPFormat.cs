﻿using Microsoft.Xna.Framework;
using RaceLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Webb
{
    public static class HTTPFormat
    {

        public static string ToHex(this Color color)
        {
            return "#" + string.Format("{0:X2}", color.R) + string.Format("{0:X2}", color.G) + string.Format("{0:X2}", color.B);
        }
    }
}
