import type { AdofaiEvent } from '../structure/interfaces';
import type { HoldMidSoundTimingRelativeTo } from '../types';

export interface SetHoldSound extends AdofaiEvent {
    eventType: 'SetHoldSound';
    holdStartSound?: string;
    holdStartSoundVolume?: number;
    holdMidSound?: string;
    holdMidSoundVolume?: number;
    holdMidSoundTiming?: number;
    holdMidSoundTimingRelativeTo?: HoldMidSoundTimingRelativeTo;
    holdEndSound?: string;
    holdEndSoundVolume?: number;
    angleOffset?: number;
}
