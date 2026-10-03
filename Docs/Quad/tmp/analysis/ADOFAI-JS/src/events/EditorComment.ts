import type { AdofaiEvent } from '../structure/interfaces';

export interface EditorComment extends AdofaiEvent {
    eventType: 'EditorComment';
    comment?: string;
    angleOffset?: number;
}
