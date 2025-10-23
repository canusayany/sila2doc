using System;

namespace Sila2DriverGen.TestConsole
{
    /// <summary>
    /// 控制台输出美化辅助类
    /// </summary>
    public static class ConsoleHelper
    {
        public static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║       SiLA2 D3驱动生成工具 - 功能测试控制台                   ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║       用于测试 '🎯 生成D3驱动' Tab 页面的完整功能             ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void PrintSection(string title)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"━━━ {title} ━━━");
            Console.ResetColor();
        }

        public static void PrintSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {message}");
            Console.ResetColor();
        }

        public static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ {message}");
            Console.ResetColor();
        }

        public static void PrintInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"ℹ {message}");
            Console.ResetColor();
        }

        public static void PrintWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"⚠ {message}");
            Console.ResetColor();
        }

        public static int ShowMenu(string title, string[] options)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(title);
            Console.ResetColor();
            Console.WriteLine();

            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine($"  {i + 1}. {options[i]}");
            }

            Console.WriteLine();
            Console.Write("请选择 (输入数字): ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= options.Length)
            {
                return choice;
            }

            return 0;
        }
    }
}

