using System;

// Direct translation of lbg.utils.Easing (decompiled AS3).
// Every function keeps the (t, b, c, d) signature so call sites read the same as the original.
public static class Easing
{
    // Easing.as:79  return param3 * (param1 = param1 / param4) * param1 + param2;
    public static double EaseInQuad(double t, double b, double c, double d)
        => c * (t /= d) * t + b;

    // Easing.as:84  return -param3 * (param1 = param1 / param4) * (param1 - 2) + param2;
    public static double EaseOutQuad(double t, double b, double c, double d)
        => -c * (t /= d) * (t - 2) + b;

    // Easing.as:89  param1 = param1 / (param4 / 2); ...
    public static double EaseBothInOutQuad(double t, double b, double c, double d)
    {
        t /= d / 2;
        if (t < 1) return c / 2 * t * t + b;
        return -c / 2 * (--t * (t - 2) - 1) + b;
    }

    // Easing.as:287  return param3 * (param1 = param1 / param4) * param1 * param1 + param2;
    public static double EaseInCubic(double t, double b, double c, double d)
        => c * (t /= d) * t * t + b;

    // Easing.as:292  return param3 * ((param1 = param1 / param4 - 1) * param1 * param1 + 1) + param2;
    public static double EaseOutCubic(double t, double b, double c, double d)
        => c * ((t = t / d - 1) * t * t + 1) + b;

    // Easing.as:44  return param3 * ((param1 = param1 / param4 - 1) * param1 * param1 * param1 * param1 + 1) + param2;
    public static double EaseOutQuint(double t, double b, double c, double d)
        => c * ((t = t / d - 1) * t * t * t * t + 1) + b;

    // Easing.as:240  return param3 * ((param1 = param1 / param4 - 1) * param1 * ((param5 + 1) * param1 + param5) + 1) + param2;
    public static double EaseOutBack(double t, double b, double c, double d, double s = 1.70158)
        => c * ((t = t / d - 1) * t * ((s + 1) * t + s) + 1) + b;
}
