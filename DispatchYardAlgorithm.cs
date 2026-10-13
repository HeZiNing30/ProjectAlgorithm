using System;

public class DispatchYardAlgorithm
{
    #region 过滤逻辑-调度场算法
    private enum TokenKind
    {
        Condition,
        And,
        Or,
        LParen,
        RParen
    }

    public static bool RangeFilterCallback(string fieldVal, string filterVal)
    {
        if (string.IsNullOrWhiteSpace(filterVal))
            return true;
        ReadOnlySpan<char> src = filterVal.Trim().AsSpan();

        // 结果栈
        Stack<bool> valStack = new();
        // 操作栈
        Stack<TokenKind> opStack = new();

        int len = src.Length;
        int segStart = 0;

        for (int pos = 0; pos < len; pos++)
        {
            char ch = src[pos];
            if (ch == '(' || ch == ')')
            {
                var pre = src[segStart..pos].Trim();
                if (pre.Length > 0)
                {
                    bool condVal = EvaluateSingleCondition(pre, fieldVal);
                    valStack.Push(condVal);
                }
                // 跳过括号
                segStart = pos + 1;

                if (ch == '(')
                {
                    opStack.Push(TokenKind.LParen);
                }
                else
                {
                    while (opStack.Count > 0 && opStack.Peek() != TokenKind.LParen)
                    {
                        if (!EvalOneOp(valStack, opStack))
                            return false;
                    }
                    if (opStack.Count > 0 && opStack.Peek() == TokenKind.LParen)
                    {
                        opStack.Pop(); //弹出左括号
                    }
                }
                continue;
            }

            if (pos > len - 2)
                continue;
            // 无优先级先进这里，识别 && / ||
            if ((ch == '&' && src[pos + 1] == '&') || (ch == '|' && src[pos + 1] == '|'))
            {
                var cond = src[segStart..pos].Trim();
                if (cond.Length > 0)
                {
                    bool condVal = EvaluateSingleCondition(cond, fieldVal);
                    valStack.Push(condVal);
                }
                segStart = pos + 2;

                var opKind = ch == '&' ? TokenKind.And : TokenKind.Or;
                // 同优先级，只要栈顶不是左括号，就先计算前面
                while (opStack.Count > 0 && opStack.Peek() != TokenKind.LParen)
                {
                    if (!EvalOneOp(valStack, opStack))
                        break; // 操作数不足，停止运算
                }
                opStack.Push(opKind);

                pos += 1; // 跳过第二个&/|
            }
        }
        // 处理循环结束后剩余的最后一段条件，即结尾非括号情况：>=30 && (<=44 || (!= 50 && <=80)) && ...否则跳过
        var lastSeg = src[segStart..].Trim();
        if (lastSeg.Length > 0)
        {
            bool condVal = EvaluateSingleCondition(lastSeg, fieldVal);
            valStack.Push(condVal);
        }

        // 剩余运算符全部计算
        while (opStack.Count > 0)
        {
            if (!EvalOneOp(valStack, opStack))
                break;
        }
        // 应只有一个结果
        return valStack.Pop();
    }
    private static bool EvalOneOp(Stack<bool> valStack, Stack<TokenKind> opStack)
    {
        if (valStack.Count < 2 || opStack.Count < 1)
        {
            return false;
        }
        var op = opStack.Pop();
        bool b = valStack.Pop();
        bool a = valStack.Pop();
        bool res = op switch
        {
            TokenKind.And => a && b,
            TokenKind.Or => a || b,
            _ => false
        };
        valStack.Push(res);
        return true;
    }
    private static bool EvaluateSingleCondition(ReadOnlySpan<char> cond, string fieldVal)
    {
        cond = cond.Trim();
        if (cond.Length == 0) return false;

        if (cond.StartsWith(">="))
        {
            var numStr = cond[2..].Trim();
            return decimal.TryParse(fieldVal, out var fv) && decimal.TryParse(numStr, out var cv) && fv >= cv;
        }
        if (cond.StartsWith("<="))
        {
            var numStr = cond[2..].Trim();
            return decimal.TryParse(fieldVal, out var fv) && decimal.TryParse(numStr, out var cv) && fv <= cv;
        }
        if (cond.StartsWith("!="))
        {
            var numStr = cond[2..].Trim();
            return decimal.TryParse(fieldVal, out var fv) && decimal.TryParse(numStr, out var cv) && fv != cv;
        }
        if (cond[0] == '>')
        {
            var numStr = cond[1..].Trim();
            return decimal.TryParse(fieldVal, out var fv) && decimal.TryParse(numStr, out var cv) && fv > cv;
        }
        if (cond[0] == '<')
        {
            var numStr = cond[1..].Trim();
            return decimal.TryParse(fieldVal, out var fv) && decimal.TryParse(numStr, out var cv) && fv < cv;
        }
        if (cond[0] == '=')
        {
            var right = cond[1..].Trim().ToString();
            return fieldVal.AsSpan().SequenceEqual(right);
        }

        // 无比较符，包含匹配
        return fieldVal.AsSpan().Contains(cond, StringComparison.Ordinal);
    }
    #endregion
}
