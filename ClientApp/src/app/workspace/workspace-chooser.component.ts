import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type Workspace = 'sales' | 'invoice' | 'trends';

@Component({
  selector: 'app-workspace-chooser',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './workspace-chooser.component.html',
  styleUrls: ['./workspace-chooser.component.css']
})
export class WorkspaceChooserComponent {
  @Input() canViewTrends = false;
  @Output() workspaceSelected = new EventEmitter<Workspace>();

  selectWorkspace(workspace: Workspace) {
    this.workspaceSelected.emit(workspace);
  }
}