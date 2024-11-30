# EVRAZ Code Review API

Добро пожаловать в **EVRAZ Code Review API**! Этот проект предназначен для того, чтобы предоставить пользователям удобный интерфейс для взаимодействия с системой автоматического Code Review.

## Требования

Перед тем как запустить проект локально, убедитесь, что у вас установлены следующие инструменты:

- **Docker** (для контейнеризации приложения)
- **Docker Compose** (для управления многоконтейнерными приложениями)
- **.NET 8.0 SDK** (для разработки)
- **Git** (для клонирования репозитория)

## Начало работы

### Клонирование репозитория

Начните с того, чтобы клонировать репозиторий на свой локальный компьютер:

```bash
git clone https://github.com/sleeping-bakery/evraz-backend.git
cd evraz-backend
cd CodeReview.Devops
docker-compose up --build -d
```
Сваггер можно увидеть по ссылке: //localhost/swagger

Для запуска тестов использовать
```bash
dotnet test
```

Для остановки приложений:
```bash
docker-compose down
```

В качестве решения было разработано монолитное .NET приложение
