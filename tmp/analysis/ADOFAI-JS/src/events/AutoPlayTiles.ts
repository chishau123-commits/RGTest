import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface AutoPlayTiles extends AdofaiEvent {
    eventType: 'AutoPlayTiles';
    editorOnly?: ABoolean;
    angleOffset?: number;
}
