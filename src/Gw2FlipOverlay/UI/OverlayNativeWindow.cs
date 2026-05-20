using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace Gw2FlipOverlay.UI;

internal sealed class OverlayNativeWindow : StandardWindow {

    private readonly Point _minimumSize;

    public OverlayNativeWindow(AsyncTexture2D background, Rectangle windowRegion, Rectangle contentRegion, Point minimumSize)
        : base(background, windowRegion, contentRegion) {
        _minimumSize = minimumSize;
    }

    protected override Point HandleWindowResize(Point newSize) {
        return new Point(
            Math.Max(_minimumSize.X, newSize.X),
            Math.Max(_minimumSize.Y, newSize.Y));
    }
}
