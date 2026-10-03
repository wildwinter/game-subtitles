#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "IGameSubtitleRenderer.h"
#include "GameSubtitlePlayer.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnGameSubtitleComplete);

/**
 * Manages paginated subtitle display driven by caller-supplied ticks.
 *
 * Create once for a given renderer and line count, then reuse across any number
 * of subtitles by calling Start() each time.
 *
 * Usage (C++):
 *   UGameSubtitlePlayer* Player = NewObject<UGameSubtitlePlayer>(this);
 *   Player->Initialize(MyRenderer, 2);
 *   Player->Start(TEXT("Hello world"), 5.0f);
 *   Player->Start(TEXT("Hello world"));  // no audio: duration estimated from the text
 *   // In your game loop / NativeTick:
 *   Player->Tick(DeltaTime);
 *
 * Usage (Blueprint):
 *   Construct Object of Class -> GameSubtitlePlayer
 *   Initialize (renderer, maxLines)
 *   Start (text, duration)    // duration 0 = estimate from the text
 *   Bind OnComplete event
 *   Call Tick every frame (or from an Actor's EventTick)
 */
UCLASS(BlueprintType, Blueprintable)
class GAMESUBTITLES_API UGameSubtitlePlayer : public UObject
{
    GENERATED_BODY()

public:
    UGameSubtitlePlayer();

    /**
     * Fired when all pages have been displayed and the animation has finished.
     * The last page remains visible until Stop() is called.
     */
    UPROPERTY(BlueprintAssignable, Category = "Subtitles")
    FOnGameSubtitleComplete OnComplete;

    /**
     * Estimates how long a subtitle should stay on screen when no duration is known.
     *
     * Counts characters (Unicode code points, ignoring U+00AD soft hyphens), divides by
     * the reading rate, and clamps the result. Counting characters rather than words means
     * CJK text, which has no spaces, still gets a sensible estimate; pass a lower
     * CharsPerSecond for languages that are read more slowly per character.
     *
     * The JS and Unity players implement the same rule with the same defaults.
     *
     * @param Text            Subtitle text; may contain U+00AD soft hyphens.
     * @param CharsPerSecond  Reading rate. Values <= 0 fall back to 14.
     * @param MinSeconds      Shortest estimate returned.
     * @param MaxSeconds      Longest estimate returned.
     * @return Estimated display time in seconds.
     */
    UFUNCTION(BlueprintPure, Category = "Subtitles")
    static float EstimateDuration(const FString& Text,
                                  float CharsPerSecond = 14.f,
                                  float MinSeconds = 1.5f,
                                  float MaxSeconds = 18.f);

    /**
     * Bind the renderer and set the initial lines-per-page value.
     * Call this once before the first Start().
     *
     * @param InRenderer  Any UObject that implements IGameSubtitleRenderer.
     * @param InMaxLines  Lines per page (>= 1). Defaults to 2.
     */
    UFUNCTION(BlueprintCallable, Category = "Subtitles")
    void Initialize(TScriptInterface<IGameSubtitleRenderer> InRenderer, int32 InMaxLines = 2);

    /**
     * Loads a subtitle, lays out text, and renders page 0 immediately.
     * Calling this while another subtitle is playing stops it first.
     *
     * @param Text                  Text; may contain U+00AD soft hyphens.
     * @param Duration              Total display seconds. When omitted or <= 0,
     *                              EstimateDuration(Text) is used instead.
     * @param CharacterName         If non-empty, "Name: " is prepended to the first line of
     *                              every page. The text is laid out with space reserved for the prefix.
     * @param bHasCharacterNameColor When true, CharacterNameColor is applied to the name prefix.
     * @param CharacterNameColor    Colour for the character name prefix.
     * @param bHasLineColor         When true, LineColor is applied to all body text lines.
     * @param LineColor             Colour for the subtitle body text on all lines.
     */
    UFUNCTION(BlueprintCallable, Category = "Subtitles")
    void Start(const FString& Text, float Duration = 0.f,
               const FString& CharacterName = TEXT(""),
               bool bHasCharacterNameColor = false,
               FLinearColor CharacterNameColor = FLinearColor::White,
               bool bHasLineColor = false,
               FLinearColor LineColor = FLinearColor::White);

    /**
     * Advances the internal clock. Call once per frame from your game loop.
     * Advances pages automatically; fires OnComplete and stops when the last page expires.
     *
     * @param DeltaSeconds  Time elapsed since the last tick.
     */
    UFUNCTION(BlueprintCallable, Category = "Subtitles")
    void Tick(float DeltaSeconds);

    /**
     * Stops playback and clears the renderer. Does not fire OnComplete.
     */
    UFUNCTION(BlueprintCallable, Category = "Subtitles")
    void Stop();

    /**
     * Clears the renderer and resets to the pre-Start state.
     * Call Start() again to replay from the beginning.
     */
    UFUNCTION(BlueprintCallable, Category = "Subtitles")
    void Reset();

    /** Number of pages in the current subtitle layout. Valid after Start(); 0 before. */
    UFUNCTION(BlueprintPure, Category = "Subtitles")
    int32 GetPageCount() const { return Pages.Num(); }

    /** Lines per page. Change takes effect on the next Start(). */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    int32 MaxLines;

private:
    TScriptInterface<IGameSubtitleRenderer> Renderer;

    TArray<TArray<FString>>        Pages;
    TArray<float>                  Timings;
    int32                          PageIndex;
    float                          Elapsed;
    bool                           bRunning;
    bool                           bDone;
    FGameSubtitleCharacterContext  CurrentCharacterContext;

    void RenderCurrent();
};
