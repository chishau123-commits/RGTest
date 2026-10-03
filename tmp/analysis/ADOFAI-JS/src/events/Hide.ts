import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface Hide extends AdofaiEvent {
    eventType: 'Hide';
    hideJudgment?: ABoolean;
    hideUI?: ABoolean;
    hidePlanet?: ABoolean;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
