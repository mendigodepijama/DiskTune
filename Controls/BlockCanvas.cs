using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiskTune.Models;

namespace DiskTune.Controls;

public class BlockCanvas : FrameworkElement
{
    private WriteableBitmap? _bitmap;
    private int _cols = 50;
    private int _rows = 50;
    private const int CellSize = 10; // 9px block + 1px border

    public int TotalBlocks => _cols * _rows;

    public BlockCanvas()
    {
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RebuildBitmap();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => RebuildBitmap();

    public void RebuildBitmap()
    {
        int w = (int)ActualWidth;
        int h = (int)ActualHeight;

        if (w <= 20 || h <= 20) return;

        _cols = Math.Max(10, w / CellSize);
        _rows = Math.Max(10, h / CellSize);

        _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
        ClearBlocks();
        InvalidateVisual();
    }

    public void ClearBlocks()
    {
        if (_bitmap == null) return;

        _bitmap.Lock();
        unsafe
        {
            uint* pBackBuffer = (uint*)_bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride / 4;
            int h = _bitmap.PixelHeight;
            int w = _bitmap.PixelWidth;

            // Fundo escuro
            uint bg = 0xFF141820;
            uint blockEmpty = 0xFF222834;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pBackBuffer[y * stride + x] = bg;
                }
            }

            // Desenha blocos vazios
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    DrawCell(pBackBuffer, stride, c, r, blockEmpty);
                }
            }
        }
        _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
        _bitmap.Unlock();
        InvalidateVisual();
    }

    public void UpdateBlock(int blockIndex, BlockHealth health)
    {
        if (_bitmap == null || blockIndex < 0 || blockIndex >= TotalBlocks) return;

        int c = blockIndex % _cols;
        int r = blockIndex / _cols;
        if (r >= _rows) return;

        uint color = health switch
        {
            BlockHealth.Good => 0xFF2ECC71,       // Verde vivo
            BlockHealth.Normal => 0xFF27AE60,     // Verde
            BlockHealth.Slow => 0xFFF1C40F,       // Amarelo
            BlockHealth.Critical => 0xFFE67E22,   // Laranja
            BlockHealth.Damaged => 0xFFE74C3C,    // Vermelho
            _ => 0xFF222834
        };

        _bitmap.Lock();
        unsafe
        {
            uint* pBackBuffer = (uint*)_bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride / 4;
            DrawCell(pBackBuffer, stride, c, r, color);
        }

        int x = c * CellSize;
        int y = r * CellSize;
        _bitmap.AddDirtyRect(new Int32Rect(x, y, CellSize, CellSize));
        _bitmap.Unlock();
    }

    private unsafe void DrawCell(uint* buffer, int stride, int col, int row, uint color)
    {
        int startX = col * CellSize;
        int startY = row * CellSize;
        int w = _bitmap!.PixelWidth;
        int h = _bitmap!.PixelHeight;

        for (int dy = 0; dy < CellSize - 2; dy++)
        {
            int py = startY + dy;
            if (py >= h) break;

            for (int dx = 0; dx < CellSize - 2; dx++)
            {
                int px = startX + dx;
                if (px >= w) break;

                buffer[py * stride + px] = color;
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_bitmap != null)
        {
            dc.DrawImage(_bitmap, new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }
}
