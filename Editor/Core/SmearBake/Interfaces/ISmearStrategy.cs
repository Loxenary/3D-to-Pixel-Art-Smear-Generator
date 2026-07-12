using SmearFramework.DataTypes;

namespace SmearFramework
{
    public interface ISmearStrategy
    {
        bool IsEnabled(PipelineConfig config);
        void Apply(PipelineContext ctx, SmearFrameData output, int frame);
    }
}
