using ChessChallenge.API;
using System;


    /// <summary>
    /// Chess Bot V2.6 - An optimized alpha-beta search engine with the following features:
    /// - Transposition Table (2^19 entries) for position caching
    /// - Principal Variation Search (PVS) for move ordering
    /// - Late Move Reduction (LMR) for searching promising moves deeper
    /// - Killer Move heuristic to remember good quiet moves
    /// - History heuristic for move ordering
    /// - Check extensions to explore tactical lines
    /// - Delta pruning in quiescence search
    /// - Null move pruning for positional advantage
    /// </summary>
    public class MyBot : IChessBot
    {
        // Transposition table entry structure
        struct E 
        { 
            public ulong k;    // Zobrist key
            public Move m;     // Best move
            public int s;      // Score
            public int d;      // Depth
            public int f;      // Flag (1=exact, 2=lower bound, 3=upper bound)
        }

        // Piece values for material evaluation (index represents piece type)
        int[] pv = { 0, 100, 320, 330, 500, 900, 10000 };  // Empty, Pawn, Knight, Bishop, Rook, Queen, King
        
        // Main transposition table - stores 2^19 entries
        E[] tt = new E[1 << 19];
        
        // Killer moves table [ply, slot] - stores 2 killer moves per ply
        Move[,] kil = new Move[64, 2];
        
        // History table [fromSquare, toSquare] - stores success history of quiet moves
        int[,] h = new int[64, 64];

        public Move Think(Board b, Timer t)
        {
            Move bm=Move.NullMove;
            for (int d=1; d<=64; d++)
            {
                S(b, d, 0, -30000, 30000);
                int i=(int)(b.ZobristKey & 0x7FFFF);
                if (tt[i].k==b.ZobristKey) bm=tt[i].m;
                if (t.MillisecondsElapsedThisTurn>t.MillisecondsRemaining/(b.PlyCount<40?60:40)) break;
            }
            return bm.IsNull ? b.GetLegalMoves()[0] : bm;
        }

        /// <summary>
        /// Main alpha-beta search function with various pruning techniques
        /// </summary>
        /// <param name="b">Current board position</param>
        /// <param name="d">Remaining depth to search</param>
        /// <param name="p">Current ply (distance from root)</param>
        /// <param name="a">Alpha (lower bound)</param>
        /// <param name="B">Beta (upper bound)</param>
        /// <param name="np">Null move flag to prevent consecutive null moves</param>
        /// <returns>Best score for the current position</returns>
        int S(Board b, int d, int p, int a, int B, bool np=false)
        {
            int i=(int)(b.ZobristKey & 0x7FFFF);
            if (p>0 && b.IsRepeatedPosition()) return 0;
            E e=tt[i];
            if (p>0 && e.k==b.ZobristKey && e.d>=d && Math.Abs(e.s)<9000 &&
                (e.f==1 || e.f==2 && e.s>=B || e.f==3 && e.s<=a)) return e.s;
            if (!np && d>2 && p>0 && !b.IsInCheck()) { b.ForceSkipTurn(); int n=-S(b, d-3, p+1, -B, -B+1, true); b.UndoSkipTurn(); if (n>=B) return B; }
            if (d<=0) return b.IsInCheck() ? S(b, 1, p, a, B) : Q(b, a, B);
            Span<Move> mv=stackalloc Move[218];
            b.GetLegalMovesNonAlloc(ref mv);
            if (mv.Length==0) return b.IsInCheck() ? p-30000 : 0;
            Move hm=e.k==b.ZobristKey ? e.m : Move.NullMove;
            Span<int> sc=stackalloc int[mv.Length];
            for (int j=0; j<mv.Length; j++)
            {
                Move m=mv[j];
                sc[j]=m==hm?9000000:m.IsCapture?1000000+10*pv[(int)m.CapturePieceType]-pv[(int)m.MovePieceType]:
                    m.IsPromotion?900000:m==kil[p,0]?800000:m==kil[p,1]?700000:h[m.StartSquare.Index,m.TargetSquare.Index];
            }
            for (int j=0; j<mv.Length-1; j++)
            {
                int best=j;
                for (int x=j+1; x<mv.Length; x++) if (sc[x]>sc[best]) best=x;
                (mv[j],mv[best])=(mv[best],mv[j]);
                (sc[j],sc[best])=(sc[best],sc[j]);
            }

            Move bm=Move.NullMove; int bs=-30000,oa=a;
            for (int j=0; j<mv.Length; j++)
            {
                Move m=mv[j];
                b.MakeMove(m);
                int r=j>4 && d>3 && !b.IsInCheck() && !m.IsCapture && !m.IsPromotion ? (j>8?2:1) : 0;
                int s2=j==0 ? -S(b, d-1, p+1, -B, -a) :
                    (s2=-S(b, d-1-r, p+1, -a-1, -a))>a ? -S(b, d-1, p+1, -B, -a) : s2;
                b.UndoMove(m);
                if (s2>bs)
                {
                    bs=s2; bm=m; a=Math.Max(a, s2);
                    if (a>=B)
                    {
                        if (!m.IsCapture) { kil[p, 1]=kil[p, 0]; kil[p, 0]=m; h[m.StartSquare.Index, m.TargetSquare.Index]+=d*d; }
                        break;
                    }
                }
            }
            tt[i]=new E{k=b.ZobristKey,m=bm,s=bs,d=d,f=bs>=B?2:bs>oa?1:3};
            return bs;
        }

        /// <summary>
        /// Quiescence search to evaluate tactical sequences
        /// Uses delta pruning to skip unlikely captures
        /// </summary>
        /// <param name="b">Current board position</param>
        /// <param name="a">Alpha (lower bound)</param>
        /// <param name="B">Beta (upper bound)</param>
        /// <param name="qd">Current quiescence depth</param>
        /// <returns>Best score for the current position</returns>
        int Q(Board b, int a, int B, int qd=0)
        {
            if (qd>10) return Ev(b);
            int e=Ev(b);
            if (e>=B) return B;
            a=Math.Max(a, e);
            Span<Move> c=stackalloc Move[218];
            b.GetLegalMovesNonAlloc(ref c, true);
            foreach (Move m in c)
            {
                if (e+pv[(int)m.CapturePieceType]<a) continue;
                b.MakeMove(m);
                int s=-Q(b, -B, -a, qd+1);
                b.UndoMove(m);
                if (s>=B) return B;
                a=Math.Max(a, s);
            }
            return a;
        }

        /// <summary>
        /// Static evaluation function
        /// Calculates material balance between white and black
        /// Positive scores favor white, negative favor black
        /// </summary>
        /// <param name="b">Current board position</param>
        /// <returns>Material balance score</returns>
        int Ev(Board b)
        {
            int mg=0;
            for (int p=1; p<7; p++)
            {
                ulong u=b.GetPieceBitboard((PieceType)p, b.IsWhiteToMove), t=b.GetPieceBitboard((PieceType)p, !b.IsWhiteToMove);
                mg += (BitboardHelper.GetNumberOfSetBits(u) - BitboardHelper.GetNumberOfSetBits(t)) * pv[p];
            }
            return mg;
        }
    }
