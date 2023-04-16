using System.Collections.Generic;

class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    NaturalComparer() { }

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public int Compare(string? x, string? y)
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    {
#pragma warning disable IDE0011 // Add braces
        if (x == null && y == null)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        int lx = x.Length, ly = y.Length;

        for (int mx = 0, my = 0; mx < lx && my < ly; mx++, my++)
        {
            if (char.IsDigit(x[mx]) && char.IsDigit(y[my]))
            {
                long vx = 0, vy = 0;

                for (; mx < lx && char.IsDigit(x[mx]); mx++)
                    vx = (vx * 10) + x[mx] - '0';

                for (; my < ly && char.IsDigit(y[my]); my++)
                    vy = (vy * 10) + y[my] - '0';

                if (vx != vy)
                    return vx > vy ? 1 : -1;
            }

            if (mx < lx && my < ly && x[mx] != y[my])
                return x[mx] > y[my] ? 1 : -1;
        }

        return lx - ly;
#pragma warning restore IDE0011 // Add braces
    }
}