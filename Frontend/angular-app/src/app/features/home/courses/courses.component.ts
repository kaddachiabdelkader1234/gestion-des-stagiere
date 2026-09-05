import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DataService } from '../../../core/services/data.service';
import { CourseDetail } from '../../../core/models/course.model';

@Component({
  selector: 'app-courses',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './courses.component.html',
  styleUrl: './courses.component.scss'
})
export class CoursesComponent implements OnInit {
  courses: CourseDetail[] = [];
  selectedCategory = 'all';

  constructor(private dataService: DataService) {}

  ngOnInit(): void {
    this.dataService.getData().subscribe(data => {
      this.courses = data.CourseDetailData;
    });
  }

  get filteredCourses(): CourseDetail[] {
    if (this.selectedCategory === 'all') {
      return this.courses;
    }
    return this.courses.filter(c => c.category === this.selectedCategory);
  }

  filterCourses(category: string): void {
    this.selectedCategory = category;
  }
}
