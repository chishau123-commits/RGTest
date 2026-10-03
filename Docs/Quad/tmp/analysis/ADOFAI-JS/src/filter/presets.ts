import { EffectCleanerType } from "./effectProcessor";

export interface Preset {
    type: EffectCleanerType | string;
    events: string[];
    [key: string]: any;
}

export const preset_noeffect: Preset = {
    type: 'exclude', events: [
        'Flash',
        'SetFilter',
        'SetFilterAdvanced',
        'HallOfMirrors',
        'Bloom',
        'ScalePlanets',
        'ScreenTile',
        'ScreenScroll',
        'ShakeScreen'
    ]
};

export const preset_noholds: Preset = {
    type: 'exclude', events: [
        'Hold',
    ]
}

export const preset_nomovecamera: Preset = {
    type: 'exclude', events: [
        'MoveCamera',
    ]
}

export const preset_noeffect_completely: Preset = {
    type: 'exclude', events: [
        "AddDecoration",
        "AddText",
        "AddObject",
        "Checkpoint",
        "SetHitsound",
        "PlaySound",
        "SetPlanetRotation",
        "ScalePlanets",
        "ColorTrack",
        "AnimateTrack",
        "RecolorTrack",
        "MoveTrack",
        "PositionTrack",
        "MoveDecorations",
        "SetText",
        "SetObject",
        "SetDefaultText",
        "CustomBackground",
        "Flash",
        "MoveCamera",
        "SetFilter",
        "HallOfMirrors",
        "ShakeScreen",
        "Bloom",
        "ScreenTile",
        "ScreenScroll",
        "SetFrameRate",
        "RepeatEvents",
        "SetConditionalEvents",
        "EditorComment",
        "Bookmark",
        "Hold",
        "SetHoldSound",
        //"MultiPlanet",
        //"FreeRoam",
        //"FreeRoamTwirl",
        //"FreeRoamRemove",
        "Hide",
        "ScaleMargin",
        "ScaleRadius"
    ]
}

export const preset_inner_no_deco: Preset = {
    type: 'special', events: [
        "MoveDecorations",
        "SetText",
        "SetObject",
        "SetDefaultText"
    ]
}