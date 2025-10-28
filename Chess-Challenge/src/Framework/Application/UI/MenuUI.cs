using Raylib_cs;
using System.Numerics;
using System;
using System.IO;

namespace ChessChallenge.Application
{
    public static class MenuUI
    {
        public static void DrawButtons(ChallengeController controller)
        {
            Vector2 buttonPos = UIHelper.Scale(new Vector2(260, 210));
            Vector2 buttonSize = UIHelper.Scale(new Vector2(260, 55));
            float spacing = buttonSize.Y * 1.2f;
            float breakSpacing = spacing * 0.6f;

            // Game Buttons
            var currentBot = BotVersionManager.GetCurrentVersion();
            if (NextButtonInRow($"Human vs {currentBot.Name}", ref buttonPos, spacing, buttonSize))
            {
                var whiteType = controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
                var blackType = !controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
                controller.StartNewGame(whiteType, blackType);
            }

            if (NextButtonInRow($"{currentBot.Name} vs EvilBot", ref buttonPos, spacing, buttonSize))
            {
                controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.EvilBot);
            }

            // Version Selector A
            var selectedBotA = BotVersionManager.GetSelectedVersion();
            string selectorTextA = $"< {selectedBotA.Name} >";

            Vector2 arrowSize = UIHelper.Scale(new Vector2(55, 55));
            Vector2 centerSize = UIHelper.Scale(new Vector2(150, 55));
            Vector2 leftArrowPos = buttonPos;
            Vector2 centerPos = new Vector2(buttonPos.X + arrowSize.X + 5, buttonPos.Y);
            Vector2 rightArrowPos = new Vector2(centerPos.X + centerSize.X + 5, buttonPos.Y);

            if (UIHelper.Button("<", leftArrowPos, arrowSize))
            {
                BotVersionManager.CycleSelectedVersion(false);
            }
            UIHelper.DrawText(selectorTextA, new Vector2(centerPos.X + centerSize.X / 2, centerPos.Y + centerSize.Y / 2),
                UIHelper.ScaleInt(32), 1, new Color(180, 180, 180, 255), UIHelper.AlignH.Centre, UIHelper.AlignV.Centre);
            if (UIHelper.Button(">", rightArrowPos, arrowSize))
            {
                BotVersionManager.CycleSelectedVersion(true);
            }
            buttonPos.Y += spacing;

            // Version Selector B
            var selectedBotB = BotVersionManager.GetSelectedVersionB();
            string selectorTextB = $"< {selectedBotB.Name} >";

            leftArrowPos = buttonPos;
            centerPos = new Vector2(buttonPos.X + arrowSize.X + 5, buttonPos.Y);
            rightArrowPos = new Vector2(centerPos.X + centerSize.X + 5, buttonPos.Y);

            if (UIHelper.Button("<", leftArrowPos, arrowSize))
            {
                BotVersionManager.CycleSelectedVersionB(false);
            }
            UIHelper.DrawText(selectorTextB, new Vector2(centerPos.X + centerSize.X / 2, centerPos.Y + centerSize.Y / 2),
                UIHelper.ScaleInt(32), 1, new Color(180, 180, 180, 255), UIHelper.AlignH.Centre, UIHelper.AlignV.Centre);
            if (UIHelper.Button(">", rightArrowPos, arrowSize))
            {
                BotVersionManager.CycleSelectedVersionB(true);
            }
            buttonPos.Y += spacing;

            // Match Button
            if (NextButtonInRow($"{selectedBotA.Name} vs {selectedBotB.Name}", ref buttonPos, spacing, buttonSize))
            {
                controller.StartBotVersionMatch(selectedBotA, selectedBotB);
            }

            // Page buttons
            buttonPos.Y += breakSpacing;

            if (NextButtonInRow("Save Games", ref buttonPos, spacing, buttonSize))
            {
                string pgns = controller.AllPGNs;
                string directoryPath = Path.Combine(FileHelper.AppDataPath, "Games");
                Directory.CreateDirectory(directoryPath);
                string fileName = FileHelper.GetUniqueFileName(directoryPath, "games", ".txt");
                string fullPath = Path.Combine(directoryPath, fileName);
                File.WriteAllText(fullPath, pgns);
                ConsoleHelper.Log("Saved games to " + fullPath, false, ConsoleColor.Blue);
            }
            if (NextButtonInRow("Rules & Help", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://github.com/SebLague/Chess-Challenge");
            }
            if (NextButtonInRow("Documentation", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://seblague.github.io/chess-coding-challenge/documentation/");
            }
            if (NextButtonInRow("Submission Page", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://forms.gle/6jjj8jxNQ5Ln53ie6");
            }

            // Window and quit buttons
            buttonPos.Y += breakSpacing;

            bool isBigWindow = Raylib.GetScreenWidth() > Settings.ScreenSizeSmall.X;
            string windowButtonName = isBigWindow ? "Smaller Window" : "Bigger Window";
            if (NextButtonInRow(windowButtonName, ref buttonPos, spacing, buttonSize))
            {
                Program.SetWindowSize(isBigWindow ? Settings.ScreenSizeSmall : Settings.ScreenSizeBig);
            }
            if (NextButtonInRow("Exit (ESC)", ref buttonPos, spacing, buttonSize))
            {
                Environment.Exit(0);
            }

            bool NextButtonInRow(string name, ref Vector2 pos, float spacingY, Vector2 size)
            {
                bool pressed = UIHelper.Button(name, pos, size);
                pos.Y += spacingY;
                return pressed;
            }
        }
    }
}
