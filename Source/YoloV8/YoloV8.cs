﻿namespace Compunet.YoloV8;

public class YoloV8 : IDisposable
{
    #region Private Memebers

    private readonly YoloV8Metadata _metadata;
    private readonly YoloV8Parameters _parameters;

    private readonly InferenceSession _inference;
    private readonly string[] _inputNames;

    private bool _disposed;

    #endregion

    #region Public Properties

    public YoloV8Metadata Metadata => _metadata;

    public YoloV8Parameters Parameters => _parameters;

    #endregion

    #region Ctors

    public YoloV8(ModelSelector selector)
        : this(selector.Load(), null, null)
    { }

    public YoloV8(ModelSelector selector, SessionOptions options)
        : this(selector.Load(), null, options)
    { }

    public YoloV8(ModelSelector selector, YoloV8Metadata metadata)
        : this(selector.Load(), metadata, null)
    { }

    public YoloV8(ModelSelector selector, YoloV8Metadata metadata, SessionOptions options)
        : this(selector.Load(), metadata, options)
    { }

    private YoloV8(byte[] model, YoloV8Metadata? metadata, SessionOptions? options)
    {
        _inference = new(model, options ?? new SessionOptions());
        _inputNames = _inference.InputMetadata.Keys.ToArray();

        _metadata = metadata ?? YoloV8Metadata.Parse(_inference.ModelMetadata.CustomMetadataMap);
        _parameters = YoloV8Parameters.Default;
    }

    #endregion

    #region Run

    public TResult Run<TResult>(ImageSelector selector, PostprocessContext<TResult> postprocess)
    {
        using var image = selector.Load(true);

        var originSize = image.Size;

        var timer = new SpeedTimer();

        timer.StartPreprocess();

        var input = Preprocess(image);

        var inputs = MapNamedOnnxValues(new Tensor<float>[] { input });

        timer.StartInference();

        using var outputs = _inference.Run(inputs);

        var list = new List<NamedOnnxValue>(outputs);

        timer.StartPostprocess();

        return postprocess(list, originSize, timer);
    }

    #endregion

    #region Private Methods

    private Tensor<float> Preprocess(Image<Rgb24> image)
    {
        var modelSize = _metadata.ImageSize;

        var dimensions = new int[] { 1, 3, modelSize.Height, modelSize.Width };
        var input = new DenseTensor<float>(dimensions);

        PreprocessHelper.ProcessToTensor(image, modelSize, _parameters.ProcessWithOriginalAspectRatio, input, 0);

        return input;
    }

    private IReadOnlyList<NamedOnnxValue> MapNamedOnnxValues(IReadOnlyList<Tensor<float>> inputs)
    {
        var length = inputs.Count;

        var values = new NamedOnnxValue[length];

        for (int i = 0; i < length; i++)
        {
            var name = _inputNames[i];

            var value = NamedOnnxValue.CreateFromTensor(name, inputs[i]);

            values[i] = value;
        }

        return values;
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed)
            return;

        _inference.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}