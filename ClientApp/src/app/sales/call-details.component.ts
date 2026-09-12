import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { SalesCall } from '../shared/models';

@Component({
  selector: 'app-call-details',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './call-details.component.html',
  styleUrl: './call-details.component.css'
})
export class CallDetailsComponent {
  @Input() call: SalesCall | null = null;
  @Input() isProspect = false;
  @Input() converting = false;

  @Output() closed = new EventEmitter<void>();
  @Output() convertRequested = new EventEmitter<void>();
  @Output() editRequested = new EventEmitter<SalesCall>();
}
