import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface SetFrameRate extends AdofaiEvent {
    eventType: 'SetFrameRate';
    frameRate?: number;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
