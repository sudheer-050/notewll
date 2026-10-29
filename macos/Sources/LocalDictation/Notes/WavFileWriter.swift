import Foundation

/// Incremental WAV writer: a placeholder header is written immediately so
/// the file is valid-shaped from the start, then PCM16 samples are appended
/// as they arrive, and `finalize()` patches the RIFF/data chunk sizes once
/// the total length is known. Always mono/16kHz/16-bit, matching what
/// `AudioCaptureEngine`/Vosk use throughout this app.
final class WavFileWriter {
    private let fileHandle: FileHandle
    private var dataByteCount: UInt32 = 0

    private static let sampleRate: UInt32 = 16000
    private static let channels: UInt16 = 1
    private static let bitsPerSample: UInt16 = 16
    private static let headerSize = 44

    init(url: URL) throws {
        FileManager.default.createFile(atPath: url.path, contents: nil)
        guard let handle = FileHandle(forWritingAtPath: url.path) else {
            throw NSError(domain: "WavFileWriter", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Could not open \(url.path) for writing."
            ])
        }
        fileHandle = handle
        // Placeholder header, patched with real sizes in finalize().
        try fileHandle.write(contentsOf: Data(repeating: 0, count: Self.headerSize))
    }

    /// `samples` are appended as raw little-endian Int16 -- Int16's native
    /// in-memory layout on both Apple Silicon and Intel is already
    /// little-endian, which is exactly what WAV requires, so no byte-swap
    /// is needed here.
    func append(samples: [Int16]) throws {
        guard !samples.isEmpty else { return }
        let data = samples.withUnsafeBufferPointer { Data(buffer: $0) }
        try fileHandle.write(contentsOf: data)
        dataByteCount += UInt32(data.count)
    }

    /// Seeks back and writes the real RIFF/data chunk sizes, then closes the
    /// file. Must be called exactly once, after all `append` calls.
    func finalize() throws {
        var header = Data(capacity: Self.headerSize)
        header.append(contentsOf: Array("RIFF".utf8))
        header.appendLittleEndian(UInt32(36 + dataByteCount))
        header.append(contentsOf: Array("WAVE".utf8))
        header.append(contentsOf: Array("fmt ".utf8))
        header.appendLittleEndian(UInt32(16))
        header.appendLittleEndian(UInt16(1)) // PCM
        header.appendLittleEndian(Self.channels)
        header.appendLittleEndian(Self.sampleRate)
        let byteRate = Self.sampleRate * UInt32(Self.channels) * UInt32(Self.bitsPerSample / 8)
        header.appendLittleEndian(byteRate)
        let blockAlign = Self.channels * (Self.bitsPerSample / 8)
        header.appendLittleEndian(blockAlign)
        header.appendLittleEndian(Self.bitsPerSample)
        header.append(contentsOf: Array("data".utf8))
        header.appendLittleEndian(dataByteCount)

        try fileHandle.seek(toOffset: 0)
        try fileHandle.write(contentsOf: header)
        try fileHandle.close()
    }
}

private extension Data {
    mutating func appendLittleEndian(_ value: UInt32) {
        var v = value.littleEndian
        append(Data(bytes: &v, count: MemoryLayout<UInt32>.size))
    }

    mutating func appendLittleEndian(_ value: UInt16) {
        var v = value.littleEndian
        append(Data(bytes: &v, count: MemoryLayout<UInt16>.size))
    }
}
