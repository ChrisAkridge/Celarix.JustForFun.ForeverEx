﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celarix.JustForFun.ForeverEx
{
    internal enum PinningMemoryState
    {
        NotPinned,
        WaitingForAddress,
        Pinned
    }

    internal enum ROMMappingMode
    {
        Mapped16,
        OverflowShifting
    }

    internal enum MathOperationKind
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        BitwiseAnd,
        BitwiseOr,
        BitwiseXor
    }

    internal enum JumpKind
    {
        Equal,
        NotEqual,
        LessThan,
        GreaterThan,
        LessThanOrEqualTo,
        GreaterThanOrEqualTo,
    }
}
