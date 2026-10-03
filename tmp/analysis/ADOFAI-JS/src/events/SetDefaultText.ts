import type { AdofaiEvent } from '../structure/interfaces';

export interface SetDefaultText extends AdofaiEvent {
    eventType: 'SetDefaultText';
    decText?: string;
    tag?: string;
    angleOffset?: number;
}
