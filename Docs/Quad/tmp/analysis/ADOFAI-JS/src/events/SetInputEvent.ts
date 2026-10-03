import type { AdofaiEvent } from '../structure/interfaces';
import type { InputEventState, InputEventTarget, ABoolean } from '../types';

export interface SetInputEvent extends AdofaiEvent {
    eventType: 'SetInputEvent';
    inputAction?: string;
    inputEventState?: InputEventState;
    inputEventTarget?: InputEventTarget;
    editorOnly?: ABoolean;
    angleOffset?: number;
}
