import type { AdofaiEvent } from '../structure/interfaces';

export interface AddParticle extends AdofaiEvent {
    eventType: 'AddParticle';
    particle?: string;
    angleOffset?: number;
}
