// Headless CI correctness check: loads the vendored Vosk model + dylib and
// transcribes a WAV file, printing the JSON result to stdout. The workflow
// feeds it audio synthesized by the runner's built-in `say` command and
// greps the output for the expected words -- this validates model loading,
// dylib linking, and basic transcription correctness with no GUI involved.
#include <stdio.h>
#include "vosk_api.h"

int main(int argc, char **argv) {
    if (argc < 3) {
        fprintf(stderr, "usage: verify_vosk <model_dir> <wav_file_16k_mono_s16le>\n");
        return 2;
    }

    vosk_set_log_level(-1);

    VoskModel *model = vosk_model_new(argv[1]);
    if (!model) {
        fprintf(stderr, "error: vosk_model_new failed for %s\n", argv[1]);
        return 1;
    }

    VoskRecognizer *recognizer = vosk_recognizer_new(model, 16000.0f);
    if (!recognizer) {
        fprintf(stderr, "error: vosk_recognizer_new failed\n");
        vosk_model_free(model);
        return 1;
    }

    FILE *wav = fopen(argv[2], "rb");
    if (!wav) {
        fprintf(stderr, "error: could not open %s\n", argv[2]);
        vosk_recognizer_free(recognizer);
        vosk_model_free(model);
        return 1;
    }
    fseek(wav, 44, SEEK_SET); // skip the standard 44-byte canonical WAV header

    short buffer[4096];
    size_t samplesRead;
    while ((samplesRead = fread(buffer, sizeof(short), 4096, wav)) > 0) {
        vosk_recognizer_accept_waveform_s(recognizer, buffer, (int)samplesRead);
    }
    fclose(wav);

    const char *result = vosk_recognizer_final_result(recognizer);
    printf("%s\n", result ? result : "{}");

    vosk_recognizer_free(recognizer);
    vosk_model_free(model);
    return 0;
}
