using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Scriptbox.SemanticKernel.Example;

/// <summary>
/// Comprehensive plugin exposing 100+ mini-APIs for performance benchmarking.
/// Tests the overhead of tool calling vs direct JS API invocation.
/// </summary>
public sealed class ManyApisPlugin
{
    // =============== Math Operations (20 APIs) ===============
    [KernelFunction("math_add")]
    [Description("Add two numbers.")]
    public Task<double> AddAsync(double a, double b) => Task.FromResult(a + b);

    [KernelFunction("math_subtract")]
    [Description("Subtract two numbers.")]
    public Task<double> SubtractAsync(double a, double b) => Task.FromResult(a - b);

    [KernelFunction("math_multiply")]
    [Description("Multiply two numbers.")]
    public Task<double> MultiplyAsync(double a, double b) => Task.FromResult(a * b);

    [KernelFunction("math_divide")]
    [Description("Divide two numbers.")]
    public Task<double> DivideAsync(double a, double b) => Task.FromResult(b != 0 ? a / b : 0);

    [KernelFunction("math_power")]
    [Description("Raise a to the power of b.")]
    public Task<double> PowerAsync(double a, double b) => Task.FromResult(Math.Pow(a, b));

    [KernelFunction("math_square_root")]
    [Description("Calculate square root.")]
    public Task<double> SquareRootAsync(double a) => Task.FromResult(Math.Sqrt(a));

    [KernelFunction("math_absolute")]
    [Description("Get absolute value.")]
    public Task<double> AbsoluteAsync(double a) => Task.FromResult(Math.Abs(a));

    [KernelFunction("math_floor")]
    [Description("Floor operation.")]
    public Task<double> FloorAsync(double a) => Task.FromResult(Math.Floor(a));

    [KernelFunction("math_ceiling")]
    [Description("Ceiling operation.")]
    public Task<double> CeilingAsync(double a) => Task.FromResult(Math.Ceiling(a));

    [KernelFunction("math_round")]
    [Description("Round to nearest integer.")]
    public Task<double> RoundAsync(double a) => Task.FromResult(Math.Round(a));

    [KernelFunction("math_min")]
    [Description("Get minimum of two numbers.")]
    public Task<double> MinAsync(double a, double b) => Task.FromResult(Math.Min(a, b));

    [KernelFunction("math_max")]
    [Description("Get maximum of two numbers.")]
    public Task<double> MaxAsync(double a, double b) => Task.FromResult(Math.Max(a, b));

    [KernelFunction("math_sin")]
    [Description("Sine function.")]
    public Task<double> SinAsync(double a) => Task.FromResult(Math.Sin(a));

    [KernelFunction("math_cos")]
    [Description("Cosine function.")]
    public Task<double> CosAsync(double a) => Task.FromResult(Math.Cos(a));

    [KernelFunction("math_tan")]
    [Description("Tangent function.")]
    public Task<double> TanAsync(double a) => Task.FromResult(Math.Tan(a));

    [KernelFunction("math_log")]
    [Description("Natural logarithm.")]
    public Task<double> LogAsync(double a) => Task.FromResult(Math.Log(a));

    [KernelFunction("math_exp")]
    [Description("Exponential function.")]
    public Task<double> ExpAsync(double a) => Task.FromResult(Math.Exp(a));

    [KernelFunction("math_modulo")]
    [Description("Modulo operation.")]
    public Task<double> ModuloAsync(double a, double b) => Task.FromResult(a % b);

    [KernelFunction("math_average")]
    [Description("Calculate average of two numbers.")]
    public Task<double> AverageAsync(double a, double b) => Task.FromResult((a + b) / 2);

    [KernelFunction("math_factorial")]
    [Description("Calculate factorial (limited to 20).")]
    public Task<long> FactorialAsync(int n)
    {
        if (n < 0 || n > 20) return Task.FromResult(0L);
        long result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return Task.FromResult(result);
    }

    // =============== String Operations (15 APIs) ===============
    [KernelFunction("str_length")]
    [Description("Get string length.")]
    public Task<int> LengthAsync(string s) => Task.FromResult(s?.Length ?? 0);

    [KernelFunction("str_uppercase")]
    [Description("Convert string to uppercase.")]
    public Task<string> UppercaseAsync(string s) => Task.FromResult(s?.ToUpperInvariant() ?? "");

    [KernelFunction("str_lowercase")]
    [Description("Convert string to lowercase.")]
    public Task<string> LowercaseAsync(string s) => Task.FromResult(s?.ToLowerInvariant() ?? "");

    [KernelFunction("str_reverse")]
    [Description("Reverse a string.")]
    public Task<string> ReverseAsync(string s)
    {
        var chars = (s ?? "").ToCharArray();
        Array.Reverse(chars);
        return Task.FromResult(new string(chars));
    }

    [KernelFunction("str_contains")]
    [Description("Check if string contains substring.")]
    public Task<bool> ContainsAsync(string s, string substring) => 
        Task.FromResult((s ?? "").Contains(substring ?? "", StringComparison.OrdinalIgnoreCase));

    [KernelFunction("str_starts_with")]
    [Description("Check if string starts with prefix.")]
    public Task<bool> StartsWithAsync(string s, string prefix) => 
        Task.FromResult((s ?? "").StartsWith(prefix ?? "", StringComparison.OrdinalIgnoreCase));

    [KernelFunction("str_ends_with")]
    [Description("Check if string ends with suffix.")]
    public Task<bool> EndsWithAsync(string s, string suffix) => 
        Task.FromResult((s ?? "").EndsWith(suffix ?? "", StringComparison.OrdinalIgnoreCase));

    [KernelFunction("str_replace")]
    [Description("Replace substring in string.")]
    public Task<string> ReplaceAsync(string s, string oldValue, string newValue) => 
        Task.FromResult((s ?? "").Replace(oldValue ?? "", newValue ?? ""));

    [KernelFunction("str_substring")]
    [Description("Extract substring.")]
    public Task<string> SubstringAsync(string s, int start, int length)
    {
        if (string.IsNullOrEmpty(s) || start >= s.Length) return Task.FromResult("");
        return Task.FromResult(s.Substring(start, Math.Min(length, s.Length - start)));
    }

    [KernelFunction("str_trim")]
    [Description("Trim whitespace from string.")]
    public Task<string> TrimAsync(string s) => Task.FromResult(s?.Trim() ?? "");

    [KernelFunction("str_split")]
    [Description("Split string by delimiter and return count.")]
    public Task<int> SplitAsync(string s, string delimiter) => 
        Task.FromResult((s ?? "").Split(delimiter ?? " ").Length);

    [KernelFunction("str_join")]
    [Description("Join two strings with separator.")]
    public Task<string> JoinAsync(string a, string b, string separator) => 
        Task.FromResult(string.Join(separator ?? " ", a ?? "", b ?? ""));

    [KernelFunction("str_index_of")]
    [Description("Find index of substring.")]
    public Task<int> IndexOfAsync(string s, string substring) => 
        Task.FromResult((s ?? "").IndexOf(substring ?? "", StringComparison.OrdinalIgnoreCase));

    [KernelFunction("str_last_index_of")]
    [Description("Find last index of substring.")]
    public Task<int> LastIndexOfAsync(string s, string substring) => 
        Task.FromResult((s ?? "").LastIndexOf(substring ?? "", StringComparison.OrdinalIgnoreCase));

    [KernelFunction("str_is_empty")]
    [Description("Check if string is empty or null.")]
    public Task<bool> IsEmptyAsync(string s) => Task.FromResult(string.IsNullOrEmpty(s));

    // =============== Array/Collection Operations (15 APIs) ===============
    [KernelFunction("array_length")]
    [Description("Get array length.")]
    public Task<int> ArrayLengthAsync(double[] arr) => Task.FromResult(arr?.Length ?? 0);

    [KernelFunction("array_sum")]
    [Description("Sum array elements.")]
    public Task<double> ArraySumAsync(double[] arr) => Task.FromResult(arr?.Sum() ?? 0);

    [KernelFunction("array_average")]
    [Description("Average array elements.")]
    public Task<double> ArrayAverageAsync(double[] arr) => Task.FromResult(arr?.Length > 0 ? arr.Average() : 0);

    [KernelFunction("array_min")]
    [Description("Find minimum in array.")]
    public Task<double> ArrayMinAsync(double[] arr) => Task.FromResult(arr?.Length > 0 ? arr.Min() : 0);

    [KernelFunction("array_max")]
    [Description("Find maximum in array.")]
    public Task<double> ArrayMaxAsync(double[] arr) => Task.FromResult(arr?.Length > 0 ? arr.Max() : 0);

    [KernelFunction("array_contains")]
    [Description("Check if array contains value.")]
    public Task<bool> ArrayContainsAsync(double[] arr, double value) => 
        Task.FromResult(arr?.Contains(value) ?? false);

    [KernelFunction("array_first")]
    [Description("Get first element.")]
    public Task<double> ArrayFirstAsync(double[] arr) => Task.FromResult(arr?.Length > 0 ? arr[0] : 0);

    [KernelFunction("array_last")]
    [Description("Get last element.")]
    public Task<double> ArrayLastAsync(double[] arr) => Task.FromResult(arr?.Length > 0 ? arr[arr.Length - 1] : 0);

    [KernelFunction("array_reverse")]
    [Description("Reverse array order.")]
    public Task<double[]> ArrayReverseAsync(double[] arr)
    {
        var copy = arr?.ToArray() ?? [];
        Array.Reverse(copy);
        return Task.FromResult(copy);
    }

    [KernelFunction("array_sort")]
    [Description("Sort array.")]
    public Task<double[]> ArraySortAsync(double[] arr)
    {
        var copy = arr?.ToArray() ?? [];
        Array.Sort(copy);
        return Task.FromResult(copy);
    }

    [KernelFunction("array_distinct_count")]
    [Description("Count distinct elements.")]
    public Task<int> ArrayDistinctCountAsync(double[] arr) => 
        Task.FromResult(arr?.Distinct().Count() ?? 0);

    [KernelFunction("array_index_of")]
    [Description("Find index of value.")]
    public Task<int> ArrayIndexOfAsync(double[] arr, double value) => 
        Task.FromResult(arr?.ToList().IndexOf(value) ?? -1);

    [KernelFunction("array_count")]
    [Description("Count occurrences of value.")]
    public Task<int> ArrayCountAsync(double[] arr, double value) => 
        Task.FromResult(arr?.Count(x => x == value) ?? 0);

    [KernelFunction("array_is_sorted")]
    [Description("Check if array is sorted.")]
    public Task<bool> ArrayIsSortedAsync(double[] arr)
    {
        if (arr?.Length <= 1) return Task.FromResult(true);
        for (int i = 1; i < arr.Length; i++)
        {
            if (arr[i] < arr[i - 1]) return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    // =============== Type/Conversion Operations (10 APIs) ===============
    [KernelFunction("conv_to_int")]
    [Description("Convert to integer.")]
    public Task<int> ToIntAsync(double value) => Task.FromResult((int)value);

    [KernelFunction("conv_to_long")]
    [Description("Convert to long.")]
    public Task<long> ToLongAsync(double value) => Task.FromResult((long)value);

    [KernelFunction("conv_to_double")]
    [Description("Convert to double.")]
    public Task<double> ToDoubleAsync(string value) => 
        Task.FromResult(double.TryParse(value, out var result) ? result : 0);

    [KernelFunction("conv_to_string")]
    [Description("Convert to string.")]
    public Task<string> ToStringAsync(double value) => Task.FromResult(value.ToString("G"));

    [KernelFunction("conv_bool_to_int")]
    [Description("Convert boolean to int (1 or 0).")]
    public Task<int> BoolToIntAsync(bool value) => Task.FromResult(value ? 1 : 0);

    [KernelFunction("conv_int_to_bool")]
    [Description("Convert int to boolean.")]
    public Task<bool> IntToBoolAsync(int value) => Task.FromResult(value != 0);

    [KernelFunction("conv_is_number")]
    [Description("Check if string is a valid number.")]
    public Task<bool> IsNumberAsync(string value) => Task.FromResult(double.TryParse(value, out _));

    [KernelFunction("conv_is_integer")]
    [Description("Check if value is an integer.")]
    public Task<bool> IsIntegerAsync(double value) => Task.FromResult(value == Math.Floor(value));

    [KernelFunction("conv_is_positive")]
    [Description("Check if value is positive.")]
    public Task<bool> IsPositiveAsync(double value) => Task.FromResult(value > 0);

    [KernelFunction("conv_is_negative")]
    [Description("Check if value is negative.")]
    public Task<bool> IsNegativeAsync(double value) => Task.FromResult(value < 0);

    // =============== Data Structure Operations (15 APIs) ===============
    [KernelFunction("dict_create_empty")]
    [Description("Create empty dictionary representation.")]
    public Task<int> DictCreateEmptyAsync() => Task.FromResult(0);

    [KernelFunction("dict_get_count")]
    [Description("Get item count in dictionary.")]
    public Task<int> DictGetCountAsync(Dictionary<string, double> dict) => 
        Task.FromResult(dict?.Count ?? 0);

    [KernelFunction("dict_has_key")]
    [Description("Check if dictionary has key.")]
    public Task<bool> DictHasKeyAsync(Dictionary<string, double> dict, string key) => 
        Task.FromResult(dict?.ContainsKey(key) ?? false);

    [KernelFunction("dict_get_value")]
    [Description("Get value from dictionary.")]
    public Task<double> DictGetValueAsync(Dictionary<string, double> dict, string key) => 
        Task.FromResult(dict?.TryGetValue(key, out var val) ?? false ? val : 0);

    [KernelFunction("dict_keys_count")]
    [Description("Count keys in dictionary.")]
    public Task<int> DictKeysCountAsync(Dictionary<string, double> dict) => 
        Task.FromResult(dict?.Keys.Count ?? 0);

    [KernelFunction("dict_values_sum")]
    [Description("Sum all values in dictionary.")]
    public Task<double> DictValuesSumAsync(Dictionary<string, double> dict) => 
        Task.FromResult(dict?.Values.Sum() ?? 0);

    [KernelFunction("dict_values_average")]
    [Description("Average all values in dictionary.")]
    public Task<double> DictValuesAverageAsync(Dictionary<string, double> dict) => 
        Task.FromResult(dict?.Values.Count > 0 ? dict.Values.Average() : 0);

    [KernelFunction("dict_max_key")]
    [Description("Get key with max value.")]
    public Task<string> DictMaxKeyAsync(Dictionary<string, double> dict) => 
        Task.FromResult(dict?.MaxBy(x => x.Value).Key ?? "");

    [KernelFunction("dict_min_key")]
    [Description("Get key with min value.")]
    public Task<string> DictMinKeyAsync(Dictionary<string, double> dict) => 
        Task.FromResult(dict?.MinBy(x => x.Value).Key ?? "");

    [KernelFunction("dict_is_empty")]
    [Description("Check if dictionary is empty.")]
    public Task<bool> DictIsEmptyAsync(Dictionary<string, double> dict) => 
        Task.FromResult(dict?.Count == 0);

    // =============== Utility/Validation Operations (10 APIs) ===============
    [KernelFunction("util_identity")]
    [Description("Identity function - returns input unchanged.")]
    public Task<double> IdentityAsync(double value) => Task.FromResult(value);

    [KernelFunction("util_negate")]
    [Description("Negate a value.")]
    public Task<double> NegateAsync(double value) => Task.FromResult(-value);

    [KernelFunction("util_increment")]
    [Description("Increment by 1.")]
    public Task<double> IncrementAsync(double value) => Task.FromResult(value + 1);

    [KernelFunction("util_decrement")]
    [Description("Decrement by 1.")]
    public Task<double> DecrementAsync(double value) => Task.FromResult(value - 1);

    [KernelFunction("util_double")]
    [Description("Double a value.")]
    public Task<double> DoubleAsync(double value) => Task.FromResult(value * 2);

    [KernelFunction("util_half")]
    [Description("Halve a value.")]
    public Task<double> HalfAsync(double value) => Task.FromResult(value / 2);

    [KernelFunction("util_percent")]
    [Description("Calculate percentage of value.")]
    public Task<double> PercentAsync(double value, double percent) => 
        Task.FromResult(value * (percent / 100));

    [KernelFunction("util_is_even")]
    [Description("Check if number is even.")]
    public Task<bool> IsEvenAsync(int value) => Task.FromResult(value % 2 == 0);

    [KernelFunction("util_is_odd")]
    [Description("Check if number is odd.")]
    public Task<bool> IsOddAsync(int value) => Task.FromResult(value % 2 != 0);

    [KernelFunction("util_is_prime")]
    [Description("Check if number is prime.")]
    public Task<bool> IsPrimeAsync(int n)
    {
        if (n < 2) return Task.FromResult(false);
        if (n == 2) return Task.FromResult(true);
        if (n % 2 == 0) return Task.FromResult(false);
        for (int i = 3; i * i <= n; i += 2)
        {
            if (n % i == 0) return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    // =============== Time Operations (5 APIs) ===============
    [KernelFunction("time_current_utc")]
    [Description("Get current UTC time in ISO format.")]
    public Task<string> CurrentUtcAsync() => Task.FromResult(DateTimeOffset.UtcNow.ToString("O"));

    [KernelFunction("time_unix_timestamp")]
    [Description("Get current Unix timestamp.")]
    public Task<long> UnixTimestampAsync() => Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    [KernelFunction("time_year")]
    [Description("Get current year.")]
    public Task<int> YearAsync() => Task.FromResult(DateTime.UtcNow.Year);

    [KernelFunction("time_month")]
    [Description("Get current month (1-12).")]
    public Task<int> MonthAsync() => Task.FromResult(DateTime.UtcNow.Month);

    [KernelFunction("time_day")]
    [Description("Get current day of month.")]
    public Task<int> DayAsync() => Task.FromResult(DateTime.UtcNow.Day);
}
