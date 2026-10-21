using System;

namespace PPBTool
{
    /// <summary>
    /// Applies a teleport to the player block and records it as a regular UndoManager turn,
    /// mirroring how FastTravel registers its move. Undo/Redo therefore keep working.
    /// </summary>
    internal sealed class TeleportService
    {
        private readonly ModConfig config;

        public TeleportService(ModConfig config)
        {
            this.config = config;
        }

        public static string Validate(TeleportCell cell)
        {
            if (World.levels == null || World.levels.Count == 0)
            {
                return "No level loaded.";
            }

            if (cell.Level == null)
            {
                return "Target level is gone.";
            }

            if (!LevelGraph.IsWalkableCell(cell.Level, cell.X, cell.Y))
            {
                return "Target cell is no longer free.";
            }

            if (World.Winning || World.EndZoom)
            {
                return "Cannot teleport while the level is being completed.";
            }

            if (World.Transitioning)
            {
                return "Cannot teleport during a level transition.";
            }

            return null;
        }

        public bool TryTeleport(TeleportCell cell, out string error)
        {
            error = Validate(cell);
            if (error != null)
            {
                return false;
            }

            Block player = GameState.Player;
            if (player == null)
            {
                error = "Player block not found.";
                return false;
            }

            try
            {
                Level target = cell.Level;

                // 1. Make sure nothing is mid-animation; otherwise the old animation would
                //    keep drawing the player in the previous level for a few frames.
                UndoManager.StopAnimations();
                ClearPhantoms();
                DrainMultiPlayerQueue();
                IfMovingStop(player);
                player.justEnteredArray = null;
                player.justInfEntered = false;
                World.ResetInspect();

                Level fromLevel = player.OuterLevel;
                int fromX = player.xpos;
                int fromY = player.ypos;
                bool fromFlipH = player.FlipH;

                if (fromLevel == target && fromX == cell.X && fromY == cell.Y)
                {
                    return true;
                }

                // The player/camera are mirrored by the XOR of the FlipH flags of every box
                // on the path root -> level. Apply only the difference between the current
                // path and the target path, so normal (non-flipped) teleports change nothing.
                bool flipDelta = LevelGraph.PathFlip(fromLevel) ^ LevelGraph.PathFlip(target);
                bool targetFlipH = fromFlipH ^ flipDelta;
                bool fromCameraFlipH = Draw.FlipH;
                bool targetCameraFlipH = fromCameraFlipH ^ flipDelta;

                // 2. Fold any half-finished turn into history so the teleport becomes its
                //    own undo step instead of being glued to the player's last action.
                FlushPendingTurn();

                bool undoable = config.Undoable;
                if (undoable)
                {
                    UndoManager.CurrentTurn.FromCameraFlipH = fromCameraFlipH;
                    UndoManager.CurrentTurn.ToCameraFlipH = targetCameraFlipH;

                    UndoManager.RegisterMove(new UndoManager.Move
                    {
                        block = player,
                        fromLevel = fromLevel,
                        fromX = fromX,
                        fromY = fromY,
                        fromFlipH = fromFlipH,
                        toLevel = target,
                        toX = cell.X,
                        toY = cell.Y,
                        toFlipH = targetFlipH,
                        dx = 0,
                        dy = 0,
                        Nudge = true,
                        AnimXScale = 1f,
                        AnimYScale = 1f,
                        AnimXOffset = 0f,
                        AnimYOffset = 0f,
                        MovedSinceRestart = player.MovedSinceRestart,
                    });
                }
                else
                {
                    UndoManager.Clear();
                }

                // 3. Perform the actual move (clears the old grid cell, sets the new one,
                //    stops animations and re-runs autotiling).
                Movement.MoveBlock(player, target, cell.X, cell.Y);
                player.FlipH = targetFlipH;
                player.DrawIsPlayer = true;
                // TurnCompletedDontPerform skips DoRedo, which is what normally sets this.
                // Without it, RestartLevel would not bring the player back to the start cell.
                player.MovedSinceRestart = true;
                Draw.FlipH = targetCameraFlipH;

                // 4. Repair all derived state that normally changes during a move.
                World.ComputeBlockLists();
                Draw.ResetCameraOffset();
                ResetEyeOffsets(player);
                Draw.receptionPerformanceHack = player.OuterLevel.hubAreaName == "Area_Reception";

                if (undoable)
                {
                    World.MoveCount++;
                    UndoManager.TurnCompletedDontPerform();
                }
                else
                {
                    World.MoveCount = 0;
                }

                World.UpdateCoveringButtonSounds(false);
                // Re-evaluate buttons/finishes: teleporting onto a finish should win the level.
                UndoManager.CheckWinCondition();
                Movement.DetectInfExitPreviews(player);

                Log.Info("Teleport " + Describe(fromLevel, fromX, fromY) + " -> "
                    + Describe(target, cell.X, cell.Y)
                    + " flipDelta=" + flipDelta + " viaBox=" + cell.ViaBox
                    + " undoable=" + undoable);
                return true;
            }
            catch (Exception ex)
            {
                error = "Teleport failed: " + ex.Message;
                Log.Error("Teleport failed", ex);
                return false;
            }
        }

        private static void FlushPendingTurn()
        {
            if (UndoManager.CurrentTurn != null && UndoManager.CurrentTurn.moves.Count > 0)
            {
                UndoManager.TurnCompletedDontPerform();
            }
        }

        private static void DrainMultiPlayerQueue()
        {
            Movement.SkipSounds = true;
            try
            {
                int guard = 0;
                while (World.multiPlayerQueued && guard++ < 64)
                {
                    World.DoNextMultiPlayerImpulse();
                }
            }
            finally
            {
                Movement.SkipSounds = false;
            }
        }

        private static void ClearPhantoms()
        {
            foreach (Level level in World.levels)
            {
                if (level != null && level.phantoms != null)
                {
                    level.phantoms.Clear();
                }
            }
        }

        private static void IfMovingStop(Block block)
        {
            if (block != null && block.Moving)
            {
                block.StopMoving();
            }
        }

        private static void ResetEyeOffsets(Block player)
        {
            var players = World.FindPlayerBlocks();
            if (players != null)
            {
                foreach (Block block in players)
                {
                    block.eyeXOffset = 0f;
                    block.eyeYOffset = 0f;
                    block.eyeXOffsetTo = 0f;
                    block.eyeYOffsetTo = 0f;
                    block.LookAnimT = 0f;
                }
            }

            if (player != null)
            {
                player.eyeXOffset = 0f;
                player.eyeYOffset = 0f;
                player.eyeXOffsetTo = 0f;
                player.eyeYOffsetTo = 0f;
                player.LookAnimT = 0f;
            }
        }

        private static string Describe(Level level, int x, int y)
        {
            string label = level == null ? "null" : level.hubAreaName;
            if (string.IsNullOrEmpty(label))
            {
                label = level == null ? "null" : level.width + "x" + level.height;
            }

            return "'" + label + "'(" + x + "," + y + ")";
        }
    }
}
