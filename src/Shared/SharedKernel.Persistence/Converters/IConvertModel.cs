namespace SharedKernel.Persistence.Converters;

public interface IConvertModel<out TTarget>
{
    TTarget Convert();
}
