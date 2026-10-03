import type { AdofaiEvent } from '../structure/interfaces';
import type { ABoolean } from '../types';

export interface Bookmark extends AdofaiEvent {
    eventType: 'Bookmark';
    bookmark?: string;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
