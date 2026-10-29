import Foundation

/// A conservative level gate, not speaker identification or a full speech
/// detector -- direct port of the real Windows app's `QuietAudio` (same
/// tuned constants), applied to the transcription input before a note's
/// final Vosk pass. Operates on raw 16kHz mono Int16 PCM.
enum QuietAudioGate {
    static let windowSampleCount = 320 // 20ms @ 16kHz mono 16-bit (640 bytes)
    static let rmsThreshold: Double = 100
    static let hysteresisWindows = 5 // +/-5 windows (+/-100ms) of context kept around a signal window
    static let minimumSignalSamples = 800 // 50ms @ 16kHz

    /// True if at least `minimumSignalSamples` samples exceed the amplitude
    /// threshold -- rejects isolated clicks/transients as "no real signal."
    static func hasSignal(_ samples: [Int16]) -> Bool {
        var count = 0
        for sample in samples where abs(Int(sample)) >= 100 {
            count += 1
            if count >= minimumSignalSamples { return true }
        }
        return false
    }

    /// Zeroes out any window that has no active (RMS >= threshold) window
    /// within +/-`hysteresisWindows` of it, preserving the original sample
    /// count/timeline -- a hysteresis-style noise gate, not a trim.
    static func filter(_ samples: [Int16]) -> [Int16] {
        guard !samples.isEmpty else { return samples }
        let windowCount = (samples.count + windowSampleCount - 1) / windowSampleCount

        var active = [Bool](repeating: false, count: windowCount)
        for window in 0..<windowCount {
            let start = window * windowSampleCount
            let end = min(start + windowSampleCount, samples.count)
            var sumSquares = 0.0
            for i in start..<end {
                let value = Double(samples[i])
                sumSquares += value * value
            }
            let rms = end > start ? (sumSquares / Double(end - start)).squareRoot() : 0
            active[window] = rms >= rmsThreshold
        }

        var keep = [Bool](repeating: false, count: windowCount)
        for window in 0..<windowCount where active[window] {
            let lo = max(0, window - hysteresisWindows)
            let hi = min(windowCount - 1, window + hysteresisWindows)
            for k in lo...hi { keep[k] = true }
        }

        var output = samples
        for window in 0..<windowCount where !keep[window] {
            let start = window * windowSampleCount
            let end = min(start + windowSampleCount, samples.count)
            for i in start..<end { output[i] = 0 }
        }
        return output
    }
}
