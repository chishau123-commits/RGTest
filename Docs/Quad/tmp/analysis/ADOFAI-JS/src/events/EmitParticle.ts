import type { AdofaiEvent } from '../structure/interfaces';

export interface EmitParticle extends AdofaiEvent {
    eventType: 'EmitParticle';
    tag?: string;
    amount?: number;
    angleOffset?: number;
}
