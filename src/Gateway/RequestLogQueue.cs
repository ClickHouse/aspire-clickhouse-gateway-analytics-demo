using System.Threading.Channels;

namespace Gateway;

internal sealed class RequestLogQueue
{
    public const int Capacity = 10_000;

    private readonly Channel<RequestLogRow> _channel = Channel.CreateBounded<RequestLogRow>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    public ChannelReader<RequestLogRow> Reader => _channel.Reader;

    public bool TryEnqueue(RequestLogRow row) => _channel.Writer.TryWrite(row);

    public void Complete() => _channel.Writer.TryComplete();
}
