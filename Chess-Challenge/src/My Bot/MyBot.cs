using ChessChallenge.API;
using System;


    public class MyBot : IChessBot
    {
        struct E { public ulong k; public Move m; public int s, d, f; }
        int[] pieceValues = { 0, 100, 320, 330, 500, 900, 10000 };
        E[] tt = new E[1 << 20];
        int[,] h = new int[64, 64];
        int prevScore;

        public Move Think(Board b, Timer t)
        {
            Move bestMove = Move.NullMove;
            for (int depth = 1; depth <= 64; depth++)
            {
                int alpha = prevScore - 20, beta = prevScore + 20;
                int score = Search(b, depth, 0, alpha, beta);
                if (Math.Abs(score - prevScore) >= 20) 
                    score = Search(b, depth, 0, -30000, 30000);
                prevScore = score;
                int index = (int)(b.ZobristKey & 0xFFFFF);
                if (tt[index].k == b.ZobristKey) bestMove = tt[index].m;
                if (t.MillisecondsElapsedThisTurn > t.MillisecondsRemaining / 40) break;
            }
            return bestMove.IsNull ? b.GetLegalMoves()[0] : bestMove;
        }

        int Search(Board b, int depth, int ply, int alpha, int beta, bool nullMoveUsed = false)
        {
            int index = (int)(b.ZobristKey & 0xFFFFF);
            if (ply > 0 && b.IsRepeatedPosition()) return 0;
            E entry = tt[index];
            bool inCheck = b.IsInCheck(), isPV = alpha + 1 != beta;
            
            if (ply > 0 && entry.k == b.ZobristKey && entry.d >= depth && Math.Abs(entry.s) < 9000 &&
                (entry.f == 1 || entry.f == 2 && entry.s >= beta || entry.f == 3 && entry.s <= alpha)) 
                return entry.s;
            
            if (depth <= 0) return inCheck ? Search(b, 1, ply, alpha, beta) : Quiesce(b, alpha, beta);
            
            int eval = Evaluate(b);
            
            // Reverse Futility Pruning
            if (!isPV && !inCheck && depth <= 3 && eval - 58 * depth >= beta) return eval;
            
            // Null Move Pruning
            if (!nullMoveUsed && depth > 2 && !inCheck && eval >= beta) 
            { 
                b.ForceSkipTurn(); 
                int nullScore = -Search(b, depth - 3, ply + 1, -beta, -beta + 1, true); 
                b.UndoSkipTurn(); 
                if (nullScore >= beta) return beta; 
            }
            
            Span<Move> moves = stackalloc Move[218];
            b.GetLegalMovesNonAlloc(ref moves);
            if (moves.Length == 0) return inCheck ? ply - 30000 : 0;
            
            Move hashMove = entry.k == b.ZobristKey ? entry.m : Move.NullMove;
            Span<int> scores = stackalloc int[moves.Length];
            
            // Move ordering
            for (int i = 0; i < moves.Length; i++)
            {
                Move m = moves[i];
                scores[i] = m == hashMove ? 9000000 : 
                           m.IsCapture ? 1000000 + 10 * pieceValues[(int)m.CapturePieceType] - pieceValues[(int)m.MovePieceType] :
                           m.IsPromotion ? 900000 : h[m.StartSquare.Index, m.TargetSquare.Index];
            }
            
            // Sort moves
            for (int i = 0; i < moves.Length - 1; i++)
            {
                int bestIdx = i;
                for (int j = i + 1; j < moves.Length; j++) 
                    if (scores[j] > scores[bestIdx]) bestIdx = j;
                (moves[i], moves[bestIdx]) = (moves[bestIdx], moves[i]);
                (scores[i], scores[bestIdx]) = (scores[bestIdx], scores[i]);
            }
            
            Move bestMove = Move.NullMove; 
            int bestScore = -30000, oldAlpha = alpha;
            
            for (int i = 0; i < moves.Length; i++)
            {
                Move m = moves[i];
                b.MakeMove(m);
                
                int extension = b.IsInCheck() ? 1 : 0;
                int reduction = i > 3 && depth > 2 && !b.IsInCheck() && !m.IsCapture && !m.IsPromotion ?
                    Math.Max(0, (i * 93 + depth * 144) / 1000 - scores[i] / 10000) : 0;
                
                int score;
                if (i == 0)
                {
                    score = -Search(b, depth - 1 + extension, ply + 1, -beta, -alpha);
                }
                else
                {
                    score = -Search(b, depth - 1 - reduction + extension, ply + 1, -alpha - 1, -alpha);
                    if (score > alpha && reduction > 0)
                        score = -Search(b, depth - 1 + extension, ply + 1, -alpha - 1, -alpha);
                    if (score > alpha && score < beta)
                        score = -Search(b, depth - 1 + extension, ply + 1, -beta, -alpha);
                }
                
                b.UndoMove(m);
                
                if (score > bestScore)
                {
                    bestScore = score; 
                    bestMove = m; 
                    alpha = Math.Max(alpha, score);
                    
                    if (alpha >= beta)
                    {
                        if (!m.IsCapture)
                        {
                            int bonus = depth * depth;
                            int startIdx = m.StartSquare.Index, targetIdx = m.TargetSquare.Index;
                            h[startIdx, targetIdx] += bonus - bonus * h[startIdx, targetIdx] / 512;
                            
                            for (int j = 0; j < i; j++)
                                if (!moves[j].IsCapture)
                                {
                                    int s1 = moves[j].StartSquare.Index, s2 = moves[j].TargetSquare.Index;
                                    h[s1, s2] -= bonus + bonus * h[s1, s2] / 512;
                                }
                        }
                        break;
                    }
                }
                
                // Futility pruning
                if (!isPV && depth <= 4 && !m.IsCapture && eval + 127 * depth < alpha) break;
            }
            
            tt[index] = new E { 
                k = b.ZobristKey, 
                m = bestMove, 
                s = bestScore, 
                d = depth, 
                f = bestScore >= beta ? 2 : bestScore > oldAlpha ? 1 : 3 
            };
            
            return bestScore;
        }

        int Quiesce(Board b, int alpha, int beta, int depth = 0)
        {
            if (depth > 10) return Evaluate(b);
            int eval = Evaluate(b);
            if (eval >= beta) return beta;
            alpha = Math.Max(alpha, eval);
            
            Span<Move> captures = stackalloc Move[218];
            b.GetLegalMovesNonAlloc(ref captures, true);
            
            foreach (Move m in captures)
            {
                if (eval + pieceValues[(int)m.CapturePieceType] + 200 < alpha) continue;
                b.MakeMove(m);
                int score = -Quiesce(b, -beta, -alpha, depth + 1);
                b.UndoMove(m);
                if (score >= beta) return beta;
                alpha = Math.Max(alpha, score);
            }
            return alpha;
        }

        int Evaluate(Board b)
        {
            int material = 0;
            for (int pieceType = 1; pieceType < 7; pieceType++)
            {
                ulong ourPieces = b.GetPieceBitboard((PieceType)pieceType, b.IsWhiteToMove);
                ulong theirPieces = b.GetPieceBitboard((PieceType)pieceType, !b.IsWhiteToMove);
                material += (BitboardHelper.GetNumberOfSetBits(ourPieces) - 
                           BitboardHelper.GetNumberOfSetBits(theirPieces)) * pieceValues[pieceType];
            }
            return material;
        }
    }
