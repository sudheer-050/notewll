import AppKit
import CoreGraphics
import CoreText
import Foundation

/// Exports a note to a real multi-page PDF using `CGPDFContext` + Core Text
/// (`CTFramesetter`/`CTFrame`) -- genuine vector/selectable text, unlike the
/// real Windows app's PDF writer, which rasterizes each page to a JPEG and
/// overlays an invisible accessibility text layer to fake selectability.
/// macOS can draw the real thing directly, so there's no need to replicate
/// that workaround here.
enum NotesPDFExporter {
    enum ExportError: Error {
        case contextCreationFailed
        case layoutStuck
    }

    private static let pageSize = CGSize(width: 612, height: 792) // US Letter, 72pt/inch
    private static let margin: CGFloat = 54

    static func export(_ note: NoteDocument, to destinationURL: URL) throws {
        let attributed = buildAttributedString(for: note)

        var mediaBox = CGRect(origin: .zero, size: pageSize)
        guard let context = CGContext(destinationURL as CFURL, mediaBox: &mediaBox, nil) else {
            throw ExportError.contextCreationFailed
        }

        // CGPDFContext's native coordinate system is already bottom-left-origin
        // Y-up, same as Core Text expects -- no coordinate flip needed as long
        // as the content rect below is expressed in that same convention.
        let contentRect = CGRect(
            x: margin,
            y: margin,
            width: pageSize.width - margin * 2,
            height: pageSize.height - margin * 2
        )
        let contentPath = CGPath(rect: contentRect, transform: nil)

        let framesetter = CTFramesetterCreateWithAttributedString(attributed)
        let totalLength = attributed.length
        var consumed = 0
        var pageNumber = 1

        while consumed < totalLength {
            let currentRange = CFRange(location: consumed, length: 0)
            let frame = CTFramesetterCreateFrame(framesetter, currentRange, contentPath, nil)

            context.beginPDFPage(nil)
            CTFrameDraw(frame, context)
            drawFooter(in: context, pageNumber: pageNumber)
            context.endPDFPage()

            let visibleRange = CTFrameGetVisibleStringRange(frame)
            guard visibleRange.length > 0 else {
                // Content rect too small to fit even one line -- bail rather
                // than loop forever re-requesting the same unconsumed range.
                throw ExportError.layoutStuck
            }
            consumed += visibleRange.length
            pageNumber += 1
        }

        context.closePDF()
    }

    private static func drawFooter(in context: CGContext, pageNumber: Int) {
        let text = "Notewll \u{00b7} Voice note snapshot \u{00b7} \(pageNumber)"
        let attributes: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 9),
            .foregroundColor: NSColor.gray
        ]
        let line = CTLineCreateWithAttributedString(NSAttributedString(string: text, attributes: attributes))
        let bounds = CTLineGetBoundsWithOptions(line, [])
        let x = (pageSize.width - bounds.width) / 2
        context.textPosition = CGPoint(x: x, y: margin / 2)
        CTLineDraw(line, context)
    }

    private static func buildAttributedString(for note: NoteDocument) -> NSAttributedString {
        let result = NSMutableAttributedString()

        let titleFont = NSFont.boldSystemFont(ofSize: 18)
        let metaFont = NSFont.systemFont(ofSize: 11)
        let bodyFont = NSFont.systemFont(ofSize: 12)

        let titleParagraph = NSMutableParagraphStyle()
        titleParagraph.paragraphSpacing = 6
        result.append(NSAttributedString(string: note.title + "\n", attributes: [
            .font: titleFont,
            .paragraphStyle: titleParagraph
        ]))

        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        let metaParagraph = NSMutableParagraphStyle()
        metaParagraph.paragraphSpacing = 14

        let metaLine = "Created: \(formatter.string(from: note.createdUtc)) \u{00b7} Mode: \(note.mode) \u{00b7} Status: \(note.status.rawValue)"
        result.append(NSAttributedString(string: metaLine + "\n", attributes: [
            .font: metaFont,
            .foregroundColor: NSColor.darkGray,
            .paragraphStyle: metaParagraph
        ]))

        if let userName = note.userName, !userName.isEmpty {
            result.append(NSAttributedString(string: "User: \(userName)\n\n", attributes: [
                .font: metaFont,
                .paragraphStyle: metaParagraph
            ]))
        }

        let bodyParagraph = NSMutableParagraphStyle()
        bodyParagraph.lineSpacing = 3
        bodyParagraph.paragraphSpacing = 10

        let transcript = note.transcriptText.isEmpty ? "(No speech was recognized.)" : note.transcriptText
        result.append(NSAttributedString(string: transcript, attributes: [
            .font: bodyFont,
            .paragraphStyle: bodyParagraph
        ]))

        return result
    }
}
