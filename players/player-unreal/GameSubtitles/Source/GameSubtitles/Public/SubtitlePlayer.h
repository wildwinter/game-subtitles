#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "ISubtitleRenderer.h"
#include "SubtitlePlayer.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnSubtitleComplete);

/**
 * Manages paginated subtitle display driven by caller-supplied ticks.
 *
 * Create once for a given renderer and line count, then reuse across any number
 * of subtitles by calling Start() each time.
 *
 * Usage (C++):
 *   USubtitlePlayer* Player = NewObject<USubtitlePlayer>(this);
 *   Player->Initialize(MyRenderer, 2);
 *   Player->Start(TEXT("Hello world"), 5.0f);
 *   // In your game loop / NativeTick:
 *   Player->Tick(DeltaTime);
 *
 * Usage (Blueprint):
 *   Construct Object of Class -> SubtitlePlayer
 *   Initialize (renderer, maxLines)
 *   Start (text, duration)
 *   Bind OnComplete event
 *   Call Tick every frame (or from an Actor's EventTick)
 */
UCLASS(BlueprintType, Blueprintable)
class GAMESUBTITLES_API USubtitlePlayer : public UObject
{
    GENERATED_BODY()

public:
    USubtitlePlayer();

    /**
     * Fired when all pages have been displayed and the animation has finished.
     * The last page remains visible until Stop() is called.
     */
    UPROPERTY(BlueprintAssignable, Category = "Subtitles")
    FOnSubtitleComplete OnComplete;

    /**
     * Bind the renderer and set the initial lines-per-page value.
     * Call this once before the first Start().
     *
     * @param InRenderer  Any UObject that implements ISubtitleRenderer.
     * @param InMaxLines  Lines per page (>= 1). Defaults to 2.
     */
    UFUNCTION(BlueprintCallable, Category = "Subtitles")
    void Initialize(TScriptInterface<ISubtitleRenderer> InRenderer, int32 InMaxLines = 2);

    /**
     * Loads a subtitle, lays out text, and renders page 0 immediately.
     * Calling this while another subtitle is playing stops it first.
     *
     * @param Text                  Text; may contain U+00AD soft hyphens.
     * @param Duration              Total display seconds (> 0).
     * @param CharacterName         If non-empty, "Name: " is prepended to the first line of
     *                              every page. The text is laid out with space reserved for the prefix.
     * @param bHasCharacterNameColor When true, CharacterNameColor is applied to the name prefix.
     * @param CharacterNameColor    Colour for the character name prefix.
     */
    UFUNCTION(BlueprintCallable, Category = "Subtitles")
    void Start(const FString& Text, float Duration,
               const FString& CharacterName = TEXT(""),
               bool bHasCharacterNameColor = false,
               FLinearColor CharacterNameColor = FLinearColor::White);

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

    /**
     * Whether the character name prefix is rendered in bold.
     * Set before calling Initialize(); takes effect on the next Start().
     */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    bool bBoldCharacterName = true;

private:
    TScriptInterface<ISubtitleRenderer> Renderer;

    TArray<TArray<FString>>    Pages;
    TArray<float>              Timings;
    int32                      PageIndex;
    float                      Elapsed;
    bool                       bRunning;
    bool                       bDone;
    FSubtitleCharacterContext  CurrentCharacterContext;

    void RenderCurrent();
};
