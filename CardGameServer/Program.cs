﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGameServer
{
    class Program
    {
        static void Main(string[] args)
        {
            GameSession game = new GameSession(4000);
            game.Run();
        }
    }
}
