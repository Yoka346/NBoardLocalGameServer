using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace NBoardLocalGameServer.Reversi
{
    using static BitBoard;

    enum DiscColor : byte
    {
        Black = 0,
        White = 1,
        Null = 2
    }

    enum Player : byte
    {
        Current = 0,
        Opponent = 1,
        Null
    }

    enum BoardCoordinate : byte
    {
        A1, B1, C1, D1, E1, F1, G1, H1,
        A2, B2, C2, D2, E2, F2, G2, H2,
        A3, B3, C3, D3, E3, F3, G3, H3,
        A4, B4, C4, D4, E4, F4, G4, H4,
        A5, B5, C5, D5, E5, F5, G5, H5,
        A6, B6, C6, D6, E6, F6, G6, H6,
        A7, B7, C7, D7, E7, F7, G7, H7,
        A8, B8, C8, D8, E8, F8, G8, H8,
        Pass, Null
    };

    struct Move
    {
        public BoardCoordinate Coord { get; set; }
        public ulong Flipped { get; set; }

        public Move(BoardCoordinate coord = BoardCoordinate.Null, ulong flipped = 0)
        {
            this.Coord = coord;
            this.Flipped = flipped;
        }
    }

    internal struct BitBoard
    {
        // ReadOnlySpan<T>プロパティにプリミティブ型の配列リテラルを指定すると, プロパティを呼び出す度にメモリの再確保はされず,
        // 静的なデータとして配列が確保されて, 実際のプロパティの呼び出しではそれが参照される(no-alloc optimization).
        // ref: https://github.com/dotnet/roslyn/pull/24621
        public static ReadOnlySpan<ulong> COORD_TO_BIT => new ulong[66]
        {
            1UL << 0, 1UL << 1, 1UL << 2, 1UL << 3, 1UL << 4, 1UL << 5, 1UL << 6, 1UL << 7,
            1UL << 8, 1UL << 9, 1UL << 10, 1UL << 11, 1UL << 12, 1UL << 13, 1UL << 14, 1UL << 15,
            1UL << 16, 1UL << 17, 1UL << 18, 1UL << 19, 1UL << 20, 1UL << 21, 1UL << 22, 1UL << 23,
            1UL << 24, 1UL << 25, 1UL << 26, 1UL << 27, 1UL << 28, 1UL << 29, 1UL << 30, 1UL << 31,
            1UL << 32, 1UL << 33, 1UL << 34, 1UL << 35, 1UL << 36, 1UL << 37, 1UL << 38, 1UL << 39,
            1UL << 40, 1UL << 41, 1UL << 42, 1UL << 43, 1UL << 44, 1UL << 45, 1UL << 46, 1UL << 47,
            1UL << 48, 1UL << 49, 1UL << 50, 1UL << 51, 1UL << 52, 1UL << 53, 1UL << 54, 1UL << 55,
            1UL << 56, 1UL << 57, 1UL << 58, 1UL << 59, 1UL << 60, 1UL << 61, 1UL << 62, 1UL << 63,
            0UL, 0UL
        };

        public ulong Player { get; set; }
        public ulong Opponent { get; set; }
        
        public ulong Discs => this.Player | this.Opponent;
        public ulong Empties => ~this.Discs;
        public int PlayerDiscCount => BitOperations.PopCount(this.Player);
        public int OpponentDiscCount => BitOperations.PopCount(this.Opponent);
        public int DiscCount => BitOperations.PopCount(this.Discs);
        public int EmptyCount => BitOperations.PopCount(this.Empties);

        public BitBoard(ulong player, ulong opponent) { this.Player = player; this.Opponent = opponent; }

        public static bool operator==(BitBoard left, BitBoard right) => left.Player == right.Player && left.Opponent == right.Opponent; 
        public static bool operator!=(BitBoard left, BitBoard right) => !(left == right);

        public override bool Equals(object? obj) => obj is BitBoard bb && this == bb;
        
        // 等価演算子をオーバーロードした場合, GetHashCodeメソッドをオーバーライドしないと警告が出る.
        public override int GetHashCode() => base.GetHashCode();

        public ulong CalculatePlayerMobility() => CalculateMobility(this.Player, this.Opponent);
        public ulong CalculateOpponentMobility() => CalculateMobility(this.Opponent, this.Player);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong CalculateFlippedDiscs(BoardCoordinate coord)
        {
            if (Avx2.X64.IsSupported)
                return CalculateFilippedDiscs_AVX2(this.Player, this.Opponent, (byte)coord);
            else if (Sse2.IsSupported)
                return CalculateFlippedDiscs_SSE(this.Player, this.Opponent, (byte)coord);
            return CalculateFlippedDiscs_CPU(this.Player, this.Opponent, (byte)coord);
        } 

        public void PutPlayerDiscAt(BoardCoordinate coord)
        {
            var bit = COORD_TO_BIT[(byte)coord];
            this.Player |= bit;

            if ((this.Opponent & bit) != 0)
                this.Opponent ^= bit;
        }

        public void PutOpponentDiscAt(BoardCoordinate coord)
        {
            var bit = COORD_TO_BIT[(byte)coord];
            this.Opponent |= bit;

            if ((this.Player & bit) != 0)
                this.Player ^= bit;
        }

        public void RemoveDiscAt(BoardCoordinate coord)
        {
            var bit = COORD_TO_BIT[(byte)coord];

            if ((this.Player & bit) != 0)
                this.Player ^= bit;

            if ((this.Opponent & bit) != 0)
                this.Opponent ^= bit;
        }

        public void Update(BoardCoordinate coord, ulong flipped)
        {
            var player = this.Player;
            this.Player = this.Opponent ^= flipped;
            this.Opponent = player | (COORD_TO_BIT[(byte)coord] | flipped);
        }

        public void Undo(BoardCoordinate coord, ulong flipped)
        {
            var player = this.Player;
            this.Player = this.Opponent ^= flipped;
            this.Opponent = player ^ (COORD_TO_BIT[(byte)coord] | flipped);
        }

        public void Swap() => (this.Player, this.Opponent) = (this.Opponent, this.Player); 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility(ulong p, ulong o)
        {
            if (Avx2.X64.IsSupported)
                return CalculateMobility_AVX2(p, o);
            else if (Sse2.IsSupported)
                return CalculateMobility_SSE(p, o);
            return CalculateMobility_CPU(p, o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility_AVX2(ulong p, ulong o)   // p is current player's board      o is opponent player's board
        {
            var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
            var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
            var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

            var flipLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(p4, shift));
            var flipRight = Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(p4, shift));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

            var mobility4 = Avx2.ShiftLeftLogicalVariable(flipLeft, shift);
            mobility4 = Avx2.Or(mobility4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
            var mobility2 = Sse2.Or(Avx2.ExtractVector128(mobility4, 0), Avx2.ExtractVector128(mobility4, 1));
            mobility2 = Sse2.Or(mobility2, Sse2.UnpackHigh(mobility2, mobility2));
            return Sse2.X64.ConvertToUInt64(mobility2) & ~(p | o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility_SSE(ulong p, ulong o)    // p is current player's board      o is opponent player's board
        {
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var p2 = Vector128.Create(p, ByteSwap(p));   // byte swap = vertical mirror
            var maskedO2 = Vector128.Create(maskedO, ByteSwap(maskedO));
            var prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 7));
            var prefix1 = maskedO & (maskedO << 1);
            var prefix8 = o & (o << 8);

            var flip = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(p2, 7));
            var flip1 = maskedO & (p << 1);
            var flip8 = o & (p << 8);
            flip = Sse2.Or(flip, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip, 7)));
            flip1 |= maskedO & (flip1 << 1);
            flip8 |= o & (flip8 << 8);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 14)));
            flip1 |= prefix1 & (flip1 << 2);
            flip8 |= prefix8 & (flip8 << 16);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 14)));
            flip1 |= prefix1 & (flip1 << 2);
            flip8 |= prefix8 & (flip8 << 16);

            var mobility2 = Sse2.ShiftLeftLogical(flip, 7);
            var mobility = (flip1 << 1) | (flip8 << 8);

            prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 9));
            prefix1 >>= 1;
            prefix8 >>= 8;
            flip = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(p2, 9));
            flip1 = maskedO & (p >> 1);
            flip8 = o & (p >> 8);
            flip = Sse2.Or(flip, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip, 9)));
            flip1 |= maskedO & (flip1 >> 1);
            flip8 |= o & (flip8 >> 8);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 18)));
            flip1 |= prefix1 & (flip1 >> 2);
            flip8 |= prefix8 & (flip8 >> 16);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 18)));
            flip1 |= prefix1 & (flip1 >> 2);
            flip8 |= prefix8 & (flip8 >> 16);
            mobility2 = Sse2.Or(mobility2, Sse2.ShiftLeftLogical(flip, 9));
            mobility |= (flip1 >> 1) | (flip8 >> 8);

            if (Sse2.X64.IsSupported)
                mobility |= Sse2.X64.ConvertToUInt64(mobility2) | ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(mobility2, mobility2)));
            else
                mobility |= mobility2.GetElement(0) | ByteSwap(Sse2.UnpackHigh(mobility2, mobility2).GetElement(0));
            return mobility & ~(p | o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility_CPU(ulong p, ulong o)    // p is current player's board      o is opponent player's board
        {
            var masked_o = o & 0x7e7e7e7e7e7e7e7eUL;

            // left
            var flip_horizontal = masked_o & (p << 1);
            var flip_diag_A1H8 = masked_o & (p << 9);
            var flip_diag_A8H1 = masked_o & (p << 7);
            var flip_vertical = o & (p << 8);

            flip_horizontal |= masked_o & (flip_horizontal << 1);
            flip_diag_A1H8 |= masked_o & (flip_diag_A1H8 << 9);
            flip_diag_A8H1 |= masked_o & (flip_diag_A8H1 << 7);
            flip_vertical |= o & (flip_vertical << 8);

            var prefix_horizontal = masked_o & (masked_o << 1);
            var prefix_diag_A1H8 = masked_o & (masked_o << 9);
            var prefix_diag_A8H1 = masked_o & (masked_o << 7);
            var prefix_vertical = o & (o << 8);

            flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
            flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 << 18);
            flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 << 14);
            flip_vertical |= prefix_vertical & (flip_vertical << 16);

            flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
            flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 << 18);
            flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 << 14);
            flip_vertical |= prefix_vertical & (flip_vertical << 16);

            var mobility = (flip_horizontal << 1) | (flip_diag_A1H8 << 9) | (flip_diag_A8H1 << 7) | (flip_vertical << 8);

            // right
            flip_horizontal = masked_o & (p >> 1);
            flip_diag_A1H8 = masked_o & (p >> 9);
            flip_diag_A8H1 = masked_o & (p >> 7);
            flip_vertical = o & (p >> 8);

            flip_horizontal |= masked_o & (flip_horizontal >> 1);
            flip_diag_A1H8 |= masked_o & (flip_diag_A1H8 >> 9);
            flip_diag_A8H1 |= masked_o & (flip_diag_A8H1 >> 7);
            flip_vertical |= o & (flip_vertical >> 8);

            prefix_horizontal = masked_o & (masked_o >> 1);
            prefix_diag_A1H8 = masked_o & (masked_o >> 9);
            prefix_diag_A8H1 = masked_o & (masked_o >> 7);
            prefix_vertical = o & (o >> 8);

            flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
            flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 >> 18);
            flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 >> 14);
            flip_vertical |= prefix_vertical & (flip_vertical >> 16);

            flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
            flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 >> 18);
            flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 >> 14);
            flip_vertical |= prefix_vertical & (flip_vertical >> 16);

            mobility |= (flip_horizontal >> 1) | (flip_diag_A1H8 >> 9) | (flip_diag_A8H1 >> 7) | (flip_vertical >> 8);
            return mobility & ~(p | o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateFilippedDiscs_AVX2(ulong p, ulong o, byte coord)    // p is current player's board      o is opponent player's board
        {
            var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
            var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
            var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

            var x = COORD_TO_BIT[coord];
            var x4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(x));
            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

            var flipLeft = Avx2.And(Avx2.ShiftLeftLogicalVariable(x4, shift), maskedO4);
            var flipRight = Avx2.And(Avx2.ShiftRightLogicalVariable(x4, shift), maskedO4);
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

            var outflankLeft = Avx2.And(p4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift));
            var outflankRight = Avx2.And(p4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
            flipLeft = Avx2.AndNot(Avx2.CompareEqual(outflankLeft, Vector256<ulong>.Zero), flipLeft);
            flipRight = Avx2.AndNot(Avx2.CompareEqual(outflankRight, Vector256<ulong>.Zero), flipRight);
            var flip4 = Avx2.Or(flipLeft, flipRight);
            var flip2 = Sse2.Or(Avx2.ExtractVector128(flip4, 0), Avx2.ExtractVector128(flip4, 1));
            flip2 = Sse2.Or(flip2, Sse2.UnpackHigh(flip2, flip2));
            return Sse2.X64.ConvertToUInt64(flip2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateFlippedDiscs_SSE(ulong p, ulong o, byte coord)    // p is current player's board      o is opponent player's board
        {
            var x = COORD_TO_BIT[coord];
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var x2 = Vector128.Create(x, ByteSwap(x));   // byte swap = vertical mirror
            var p2 = Vector128.Create(p, ByteSwap(p));
            var maskedO2 = Vector128.Create(maskedO, ByteSwap(maskedO));
            var prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 7));
            var prefix1 = maskedO & (maskedO << 1);
            var prefix8 = o & (o << 8);

            var flip7 = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(x2, 7));
            var flip1Left = maskedO & (x << 1);
            var flip8Left = o & (x << 8);
            flip7 = Sse2.Or(flip7, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip7, 7)));
            flip1Left |= maskedO & (flip1Left << 1);
            flip8Left |= o & (flip8Left << 8);
            flip7 = Sse2.Or(flip7, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip7, 14)));
            flip1Left |= prefix1 & (flip1Left << 2);
            flip8Left |= prefix8 & (flip8Left << 16);
            flip7 = Sse2.Or(flip7, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip7, 14)));
            flip1Left |= prefix1 & (flip1Left << 2);
            flip8Left |= prefix8 & (flip8Left << 16);

            prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 9));
            prefix1 >>= 1;
            prefix8 >>= 8;

            var flip9 = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(x2, 9));
            var flip1Right = maskedO & (x >> 1);
            var flip8Right = o & (x >> 8);
            flip9 = Sse2.Or(flip9, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip9, 9)));
            flip1Right |= maskedO & (flip1Right >> 1);
            flip8Right |= o & (flip8Right >> 8);
            flip9 = Sse2.Or(flip9, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip9, 18)));
            flip1Right |= prefix1 & (flip1Right >> 2);
            flip8Right |= prefix8 & (flip8Right >> 16);
            flip9 = Sse2.Or(flip9, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip9, 18)));
            flip1Right |= prefix1 & (flip1Right >> 2);
            flip8Right |= prefix8 & (flip8Right >> 16);

            var outflank7 = Sse2.And(p2, Sse2.ShiftLeftLogical(flip7, 7));
            var outflankLeft1 = p & (flip1Left << 1);
            var outflankLeft8 = p & (flip8Left << 8);
            var outflank9 = Sse2.And(p2, Sse2.ShiftLeftLogical(flip9, 9));
            var outflankRight1 = p & (flip1Right >> 1);
            var outflankRight8 = p & (flip8Right >> 8);

            if (Sse41.IsSupported)
            {
                flip7 = Sse2.AndNot(Sse41.CompareEqual(outflank7, Vector128<ulong>.Zero), flip7);
                flip9 = Sse2.AndNot(Sse41.CompareEqual(outflank9, Vector128<ulong>.Zero), flip9);
            }
            else
            {
                flip7 = Sse2.And(Sse2.CompareNotEqual(outflank7.AsDouble(), Vector128<ulong>.Zero.AsDouble()).AsUInt64(), flip7);
                flip9 = Sse2.And(Sse2.CompareNotEqual(outflank9.AsDouble(), Vector128<ulong>.Zero.AsDouble()).AsUInt64(), flip9);
            }

            if (outflankLeft1 == 0)
                flip1Left = 0UL;
            if (outflankLeft8 == 0)
                flip8Left = 0UL;
            if (outflankRight1 == 0)
                flip1Right = 0UL;
            if (outflankRight8 == 0)
                flip8Right = 0UL;

            var flippedDiscs2 = Sse2.Or(flip7, flip9);
            var flippedDiscs = flip1Left | flip8Left | flip1Right | flip8Right;
            if (Sse2.X64.IsSupported)
                flippedDiscs |= Sse2.X64.ConvertToUInt64(flippedDiscs2)
                             | ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2)));
            else
                flippedDiscs |= flippedDiscs2.GetElement(0) | ByteSwap(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2).GetElement(0));
            return flippedDiscs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateFlippedDiscs_CPU(ulong p, ulong o, byte coord)    // p is current player's board      o is opponent player's board
        {
            var coord_bit = COORD_TO_BIT[coord];
            var masked_o = o & 0x7e7e7e7e7e7e7e7eUL;

            // left
            var flipped_horizontal = masked_o & (coord_bit << 1);
            var flipped_diag_A1H8 = masked_o & (coord_bit << 9);
            var flipped_diag_A8H1 = masked_o & (coord_bit << 7);
            var flipped_vertical = o & (coord_bit << 8);

            flipped_horizontal |= masked_o & (flipped_horizontal << 1);
            flipped_diag_A1H8 |= masked_o & (flipped_diag_A1H8 << 9);
            flipped_diag_A8H1 |= masked_o & (flipped_diag_A8H1 << 7);
            flipped_vertical |= o & (flipped_vertical << 8);

            var prefix_horizontal = masked_o & (masked_o << 1);
            var prefix_diag_A1H8 = masked_o & (masked_o << 9);
            var prefix_diag_A8H1 = masked_o & (masked_o << 7);
            var prefix_vertical = o & (o << 8);

            flipped_horizontal |= prefix_horizontal & (flipped_horizontal << 2);
            flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 << 18);
            flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 << 14);
            flipped_vertical |= prefix_vertical & (flipped_vertical << 16);

            flipped_horizontal |= prefix_horizontal & (flipped_horizontal << 2);
            flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 << 18);
            flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 << 14);
            flipped_vertical |= prefix_vertical & (flipped_vertical << 16);

            var outflank_horizontal = p & (flipped_horizontal << 1);
            var outflank_diag_A1H8 = p & (flipped_diag_A1H8 << 9);
            var outflank_diag_A8H1 = p & (flipped_diag_A8H1 << 7);
            var outflank_vertical = p & (flipped_vertical << 8);

            if (outflank_horizontal == 0UL)
                flipped_horizontal = 0UL;

            if (outflank_diag_A1H8 == 0UL)
                flipped_diag_A1H8 = 0UL;

            if (outflank_diag_A8H1 == 0UL)
                flipped_diag_A8H1 = 0UL;

            if (outflank_vertical == 0UL)
                flipped_vertical = 0UL;

            var flipped = flipped_horizontal | flipped_diag_A1H8 | flipped_diag_A8H1 | flipped_vertical;

            // right
            flipped_horizontal = masked_o & (coord_bit >> 1);
            flipped_diag_A1H8 = masked_o & (coord_bit >> 9);
            flipped_diag_A8H1 = masked_o & (coord_bit >> 7);
            flipped_vertical = o & (coord_bit >> 8);

            flipped_horizontal |= masked_o & (flipped_horizontal >> 1);
            flipped_diag_A1H8 |= masked_o & (flipped_diag_A1H8 >> 9);
            flipped_diag_A8H1 |= masked_o & (flipped_diag_A8H1 >> 7);
            flipped_vertical |= o & (flipped_vertical >> 8);

            prefix_horizontal = masked_o & (masked_o >> 1);
            prefix_diag_A1H8 = masked_o & (masked_o >> 9);
            prefix_diag_A8H1 = masked_o & (masked_o >> 7);
            prefix_vertical = o & (o >> 8);

            flipped_horizontal |= prefix_horizontal & (flipped_horizontal >> 2);
            flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 >> 18);
            flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 >> 14);
            flipped_vertical |= prefix_vertical & (flipped_vertical >> 16);

            flipped_horizontal |= prefix_horizontal & (flipped_horizontal >> 2);
            flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 >> 18);
            flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 >> 14);
            flipped_vertical |= prefix_vertical & (flipped_vertical >> 16);

            var outflank_horizontal_right = p & (flipped_horizontal >> 1);
            var outflank_diag_A1H8_right = p & (flipped_diag_A1H8 >> 9);
            var outflank_diag_A8H1_right = p & (flipped_diag_A8H1 >> 7);
            var outflank_vertical_right = p & (flipped_vertical >> 8);

            if (outflank_horizontal_right == 0UL)
                flipped_horizontal = 0UL;

            if (outflank_diag_A1H8_right == 0UL)
                flipped_diag_A1H8 = 0UL;

            if (outflank_diag_A8H1_right == 0UL)
                flipped_diag_A8H1 = 0UL;

            if (outflank_vertical_right == 0UL)
                flipped_vertical = 0UL;

            return flipped | flipped_horizontal | flipped_diag_A1H8 | flipped_diag_A8H1 | flipped_vertical;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ByteSwap(ulong bits)
        {
            var ret = bits << 56;
            ret |= (bits & 0x000000000000ff00) << 40;
            ret |= (bits & 0x0000000000ff0000) << 24;
            ret |= (bits & 0x00000000ff000000) << 8;
            ret |= (bits & 0x000000ff00000000) >> 8;
            ret |= (bits & 0x0000ff0000000000) >> 24;
            ret |= (bits & 0x00ff000000000000) >> 40;
            return ret | (bits >> 56);
        }
    }

    /// <summary>
    /// リバーシの盤面.
    /// </summary>
    internal class Position
    {
        public const int BOARD_SIZE = 8;
        public const int SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;

        public DiscColor SideToMove 
        { 
            get => this.sideToMove;
            set
            {
                if (value != this.sideToMove)
                    Pass();
                this.sideToMove = value;
            }
        }

        public DiscColor OpponentColor => this.sideToMove ^ DiscColor.White; 
        public int EmptySquareCount => this.bitboard.EmptyCount;
        public int PlayerDiscCount => this.bitboard.PlayerDiscCount;
        public int OpponentDiscCount => this.bitboard.OpponentDiscCount; 
        public int DiscCount => this.bitboard.DiscCount;

        public bool CanPass => BitOperations.PopCount(this.bitboard.CalculatePlayerMobility()) == 0
                            && BitOperations.PopCount(this.bitboard.CalculateOpponentMobility()) != 0;

        public bool IsGameOver => BitOperations.PopCount(this.bitboard.CalculatePlayerMobility()) == 0
                               && BitOperations.PopCount(this.bitboard.CalculateOpponentMobility()) == 0;

        BitBoard bitboard;
        DiscColor sideToMove;
        readonly Stack<Move> moveHistory = new();

        public Position()
        {
            // デフォルトはクロス配置.
            this.bitboard = new BitBoard(COORD_TO_BIT[(byte)BoardCoordinate.E4] | COORD_TO_BIT[(byte)BoardCoordinate.D5],
                                         COORD_TO_BIT[(byte)BoardCoordinate.D4] | COORD_TO_BIT[(byte)BoardCoordinate.E5]);
            this.sideToMove = DiscColor.Black;
        }

        public Position(BitBoard bitboard, DiscColor sideToMove)
        {
            this.bitboard = bitboard;
            this.sideToMove = sideToMove;
        }

        public static bool operator ==(Position left, Position right) => left.sideToMove == right.sideToMove && left.bitboard == right.bitboard;
        public static bool operator !=(Position left, Position right) => !(left == right);

        public override bool Equals(object? obj) => obj is Position pos && this == pos;

        // 警告抑制のためのコード.
        public override int GetHashCode() => base.GetHashCode();

        public int GetDiscCountOf(DiscColor color)
        {
            if (color == DiscColor.Null)
                return 0;

            return (this.sideToMove == color) ? this.PlayerDiscCount : this.OpponentDiscCount;
        }

        /// <summary>
        /// 指定された座標にある石が現在の手番の石なのか, 相手の石なのか, それとも石が存在しないのかを返す.
        /// </summary>
        /// <param name="coord"></param>
        /// <returns></returns>
        public Player GetSquareOwnerAt(BoardCoordinate coord) 
            => (Player)(2 - 2 * ((this.bitboard.Player >> (byte)coord) & 1) - ((this.bitboard.Opponent >> (byte)coord) & 1));

        public DiscColor GetSquareColorAt(BoardCoordinate coord)
        {
            var owner = GetSquareOwnerAt(coord);
            if (owner == Player.Null)
                return DiscColor.Null;
            return (owner == Player.Current) ? this.sideToMove : this.OpponentColor;
        }

        public bool IsLegal(BoardCoordinate coord) 
            => (coord == BoardCoordinate.Pass) ? this.CanPass : ((this.bitboard.CalculatePlayerMobility() & COORD_TO_BIT[(byte)coord]) != 0);

        public void Pass()
        {
            this.sideToMove = this.OpponentColor;
            this.bitboard.Swap();
            this.moveHistory.Push(new Move(BoardCoordinate.Pass));
        }

        public void PutPlayerDiscAt(BoardCoordinate coord) => this.bitboard.PutPlayerDiscAt(coord);
        public void PutOpponentDiscAt(BoardCoordinate coord) => this.bitboard.PutOpponentDiscAt(coord);

        public void PutDiscAt(DiscColor color, BoardCoordinate coord)
        {
            if (color == DiscColor.Null)
                return;

            if (this.sideToMove == color)
                PutPlayerDiscAt(coord);
            else
                PutOpponentDiscAt(coord);
        }

        public void RemoveDiscAt(BoardCoordinate coord) => this.bitboard.RemoveDiscAt(coord);

        public bool Update(BoardCoordinate move)
        {
            if(!IsLegal(move))
                return false;

            var m = new Move
            {
                Coord = move,
                Flipped = this.bitboard.CalculateFlippedDiscs(move)
            };
            this.sideToMove = this.OpponentColor;
            this.bitboard.Update(m.Coord, m.Flipped);
            this.moveHistory.Push(m);
            return true;
        }

        public bool Undo()
        {
            if (this.moveHistory.Count == 0)
                return false;

            this.sideToMove = this.OpponentColor;
            var move = this.moveHistory.Pop();
            this.bitboard.Undo(move.Coord, move.Flipped);
            return true;
        }
    }
}
