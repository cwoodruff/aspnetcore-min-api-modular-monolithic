using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Extensions;

public static class ConvertExtensions
{
    public static IEnumerable<TTarget> ConvertAll<TTarget>(
        this IEnumerable<IConvertModel<TTarget>> values)
    {
        return values.Select(value => value.Convert());
    }
}
