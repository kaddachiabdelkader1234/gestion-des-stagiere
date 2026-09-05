import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../../core/services/data.service';
import { Mentor } from '../../../core/models/mentor.model';

@Component({
  selector: 'app-mentors',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './mentors.component.html',
  styleUrl: './mentors.component.scss'
})
export class MentorsComponent implements OnInit {
  mentors: Mentor[] = [];

  constructor(private dataService: DataService) {}

  ngOnInit(): void {
    this.dataService.getData().subscribe(data => {
      this.mentors = data.MentorData;
    });
  }
}
