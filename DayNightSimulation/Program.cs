using System;

namespace DayNightSimulation
{
    class Program
    {
        static void Main(string[] args)
        {
            using (Game game = new Game(800, 600, "Day & Night Simulation"))
            {
                game.Run(60.0); // 60 FPS
            }
        }
    }
}
