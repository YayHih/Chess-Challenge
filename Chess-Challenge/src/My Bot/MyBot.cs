using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    struct TTEntry { public ulong key; public Move move; public int score, depth, flag; }

    int[] pieceVals = { 0, 100, 320, 330, 500, 900, 10000 };

    TTEntry[] tt = new TTEntry[1 << 18];
    Move[,] killers = new Move[32, 2];

    public Move Think(Board board, Timer timer)
    {
        Move best = Move.NullMove;
        int timeLimit = Math.Min(timer.MillisecondsRemaining / 40, 500);

        for (int d = 1; d <= 32; d++)
        {
            Search(board, d, -99999, 99999, 0);
            int idx = (int)(board.ZobristKey % (ulong)tt.Length);
            if (tt[idx].key == board.ZobristKey) best = tt[idx].move;
            if (timer.MillisecondsElapsedThisTurn > timeLimit) break;
        }

        return best.IsNull ? board.GetLegalMoves()[0] : best;
    }

    int Search(Board b, int d, int a, int beta, int ply)
    {
        if (b.IsDraw()) return 0;
        bool check = b.IsInCheck();
        if (check) d++;
        if (d <= 0) return Quiesce(b, a, beta);

        ulong key = b.ZobristKey;
        int idx = (int)(key % (ulong)tt.Length);
        TTEntry e = tt[idx];
        Move ttMove = e.key == key ? e.move : Move.NullMove;

        if (e.key == key && e.depth >= d && e.flag == 0) return e.score;

        Span<Move> moves = stackalloc Move[218];
        b.GetLegalMovesNonAlloc(ref moves);
        if (moves.Length == 0) return check ? -99999 + ply : 0;

        Order(moves, ttMove, ply);

        Move bestMove = Move.NullMove;
        int best = -99999;

        foreach (Move m in moves)
        {
            b.MakeMove(m);
            int score = -Search(b, d - 1, -beta, -a, ply + 1);
            b.UndoMove(m);

            if (score > best)
            {
                best = score;
                bestMove = m;

                if (score > a)
                {
                    a = score;

                    if (a >= beta)
                    {
                        if (!m.IsCapture)
                        {
                            killers[ply, 1] = killers[ply, 0];
                            killers[ply, 0] = m;
                        }
                        break;
                    }
                }
            }
        }

        tt[idx] = new TTEntry { key = key, move = bestMove, score = best, depth = d, flag = 0 };

        return best;
    }

    int Quiesce(Board b, int a, int beta)
    {
        int stand = Eval(b);
        if (stand >= beta) return beta;
        if (a < stand) a = stand;

        Span<Move> caps = stackalloc Move[218];
        b.GetLegalMovesNonAlloc(ref caps, true);
        Order(caps, Move.NullMove, 0);

        foreach (Move c in caps)
        {
            b.MakeMove(c);
            int score = -Quiesce(b, -beta, -a);
            b.UndoMove(c);
            if (score >= beta) return beta;
            if (score > a) a = score;
        }
        return a;
    }

    void Order(Span<Move> moves, Move ttm, int ply)
    {
        Span<int> scores = stackalloc int[moves.Length];

        for (int i = 0; i < moves.Length; i++)
        {
            Move m = moves[i];
            scores[i] = m == ttm ? 1000000 :
                        m.IsCapture ? 10000 + pieceVals[(int)m.CapturePieceType] * 10 - pieceVals[(int)m.MovePieceType] :
                        m.IsPromotion ? 9000 :
                        m == killers[ply, 0] ? 8000 :
                        m == killers[ply, 1] ? 7000 : 0;
        }

        for (int i = 0; i < moves.Length - 1; i++)
        {
            int best = i;
            for (int j = i + 1; j < moves.Length; j++)
                if (scores[j] > scores[best]) best = j;
            if (best != i)
            {
                (moves[i], moves[best]) = (moves[best], moves[i]);
                (scores[i], scores[best]) = (scores[best], scores[i]);
            }
        }
    }

    int Eval(Board b)
    {
        int mg = 0;

        for (int p = 1; p <= 6; p++)
        {
            ulong w = b.GetPieceBitboard((PieceType)p, true);
            ulong bl = b.GetPieceBitboard((PieceType)p, false);
            mg += (BitboardHelper.GetNumberOfSetBits(w) - BitboardHelper.GetNumberOfSetBits(bl)) * pieceVals[p];
        }

        return b.IsWhiteToMove ? mg : -mg;
    }
}
