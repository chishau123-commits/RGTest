import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface PlaySound extends AdofaiEvent {
    eventType: 'PlaySound';
    sound?: string;
    volume?: number;
    pan?: number;
    loop?: ABoolean;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
