import AppKit
import Foundation

let arguments = CommandLine.arguments

guard arguments.count == 3 else {
    fputs("Usage: render-icon.swift <glyph.png> <output.png>\n", stderr)
    exit(64)
}

let glyphURL = URL(fileURLWithPath: arguments[1])
let outputURL = URL(fileURLWithPath: arguments[2])

guard let glyph = NSImage(contentsOf: glyphURL) else {
    fputs("Could not read icon glyph at \(glyphURL.path)\n", stderr)
    exit(66)
}

let iconSize = NSSize(width: 1024, height: 1024)
let bounds = NSRect(origin: .zero, size: iconSize)

guard
    let bitmap = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: Int(iconSize.width),
        pixelsHigh: Int(iconSize.height),
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
    ),
    let graphicsContext = NSGraphicsContext(bitmapImageRep: bitmap)
else {
    fputs("Could not create icon render context\n", stderr)
    exit(70)
}

bitmap.size = iconSize
graphicsContext.imageInterpolation = .high

NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = graphicsContext

let context = graphicsContext.cgContext

let backgroundPath = NSBezierPath(roundedRect: bounds, xRadius: 224, yRadius: 224)
backgroundPath.addClip()

let colorSpace = CGColorSpace(name: CGColorSpace.displayP3) ?? CGColorSpaceCreateDeviceRGB()
let colors = [
    NSColor(displayP3Red: 0.23193, green: 0.41350, blue: 0.80588, alpha: 1).cgColor,
    NSColor(displayP3Red: 0.38860, green: 0.77688, blue: 0.96841, alpha: 1).cgColor,
] as CFArray

if let gradient = CGGradient(colorsSpace: colorSpace, colors: colors, locations: [0, 1]) {
    context.drawLinearGradient(
        gradient,
        start: CGPoint(x: bounds.midX, y: bounds.minY),
        end: CGPoint(x: bounds.midX, y: bounds.maxY),
        options: []
    )
}

context.saveGState()
context.setShadow(
    offset: CGSize(width: 0, height: -28),
    blur: 26,
    color: NSColor.black.withAlphaComponent(0.5).cgColor
)
glyph.draw(in: bounds, from: .zero, operation: .sourceOver, fraction: 0.88)
context.restoreGState()

NSColor.white.withAlphaComponent(0.16).setFill()
backgroundPath.fill()

NSGraphicsContext.restoreGraphicsState()

guard let pngData = bitmap.representation(using: .png, properties: [:]) else {
    fputs("Could not encode rendered icon as PNG\n", stderr)
    exit(70)
}

do {
    try pngData.write(to: outputURL, options: .atomic)
} catch {
    fputs("Could not write rendered icon to \(outputURL.path): \(error)\n", stderr)
    exit(73)
}
