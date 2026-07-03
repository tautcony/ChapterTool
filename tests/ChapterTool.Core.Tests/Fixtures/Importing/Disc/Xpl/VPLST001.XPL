<?xml version="1.0" encoding="UTF-8"?>
<Playlist xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
 xsi:schemaLocation="http://www.dvdforum.org/2005/HDDVDVideo/Playlist file:/F:/Program%20Files/Oxygen%207.0/current_schemas/Playlist.xsd"
 xmlns="http://www.dvdforum.org/2005/HDDVDVideo/Playlist"
 displayName="Enter the Dragon HD DVD"
 minorVersion="0"
 majorVersion="1">

	<Configuration>
		<StreamingBuffer size="0"/>
		<Aperture size="1920x1080"/>
		<MainVideoDefaultColor color="108080"/>
	</Configuration>

	<MediaAttributeList>
		<VideoAttributeItem codec="VC-1" index="1"/>
		<VideoAttributeItem codec="MPEG-2" index="2"/>
		<AudioAttributeItem codec="DD+" index="1" channels="6"/>
		<AudioAttributeItem codec="DD+" index="2" channels="2"/>
		<AudioAttributeItem codec="DD+" index="3" channels="1"/>
		<SubpictureAttributeItem codec="8bitRLC" index="1"/>
	</MediaAttributeList>

	<TitleSet tickBase="60fps" timeBase="60fps" defaultLanguage="en">

	
		<Title displayName="Feature Presentation" id="feature" titleNumber="2"
			titleDuration="01:42:50:14" tickBaseDivisor="4" onEnd="black">
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/PEVOB_1.MAP"
				titleTimeBegin="00:00:00:00" titleTimeEnd="00:44:31:30" 
				dataSource="Disc">
				<Video mediaAttr="1" track="1"/>
				<Audio mediaAttr="1" track="1" description="English 5.1" streamNumber="1"/>
				<Audio mediaAttr="3" track="2" description="French 1.0" streamNumber="2"/>
				<Audio mediaAttr="2" track="3" description="Spanish 2.0" streamNumber="3"/>
				<Audio mediaAttr="2" track="4" description="Commentary" streamNumber="4"/>
				<Subtitle mediaAttr="1" track="1" description="English" streamNumber="1"/>
				<Subtitle mediaAttr="1" track="2" description="English SDH" streamNumber="2"/>
				<Subtitle mediaAttr="1" track="3" description="French" streamNumber="3"/>
				<Subtitle mediaAttr="1" track="4" description="Spanish" streamNumber="4"/>
			</PrimaryAudioVideoClip>
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/PEVOB_2.MAP"
				titleTimeBegin="00:44:31:30" titleTimeEnd="01:42:27:00" 
				dataSource="Disc" seamless="true">
				<Video mediaAttr="1" track="1"/>
				<Audio mediaAttr="1" track="1" description="English 5.1" streamNumber="1"/>
				<Audio mediaAttr="3" track="2" description="French 1.0" streamNumber="2"/>
				<Audio mediaAttr="2" track="3" description="Spanish 2.0" streamNumber="3"/>
				<Audio mediaAttr="2" track="4" description="Commentary" streamNumber="4"/>
				<Subtitle mediaAttr="1" track="1" description="English" streamNumber="1"/>
				<Subtitle mediaAttr="1" track="2" description="English SDH" streamNumber="2"/>
				<Subtitle mediaAttr="1" track="3" description="French" streamNumber="3"/>
				<Subtitle mediaAttr="1" track="4" description="Spanish" streamNumber="4"/>
			</PrimaryAudioVideoClip>
			
			<ChapterList>
				<Chapter displayName="Chapter 1" titleTimeBegin="00:00:00:00" />
				<Chapter displayName="Chapter 2" titleTimeBegin="00:02:29:10" />
				<Chapter displayName="Chapter 3" titleTimeBegin="00:05:28:10" />
				<Chapter displayName="Chapter 4" titleTimeBegin="00:08:25:50" />
				<Chapter displayName="Chapter 5" titleTimeBegin="00:10:40:00" />
				<Chapter displayName="Chapter 6" titleTimeBegin="00:14:38:20" />
				<Chapter displayName="Chapter 7" titleTimeBegin="00:20:15:00" />
				<Chapter displayName="Chapter 8" titleTimeBegin="00:25:06:50" />
				<Chapter displayName="Chapter 9" titleTimeBegin="00:27:39:20" />
				<Chapter displayName="Chapter 10" titleTimeBegin="00:31:32:10" />
				<Chapter displayName="Chapter 11" titleTimeBegin="00:34:01:50" />
				<Chapter displayName="Chapter 12" titleTimeBegin="00:38:57:50" />
				<Chapter displayName="Chapter 13" titleTimeBegin="00:41:51:10" />
				<Chapter displayName="Chapter 14" titleTimeBegin="00:45:18:20" />
				<Chapter displayName="Chapter 15" titleTimeBegin="00:46:32:40" />
				<Chapter displayName="Chapter 16" titleTimeBegin="00:50:45:10" />
				<Chapter displayName="Chapter 17" titleTimeBegin="00:55:08:10" />
				<Chapter displayName="Chapter 18" titleTimeBegin="00:57:52:50" />
				<Chapter displayName="Chapter 19" titleTimeBegin="01:00:32:30" />
				<Chapter displayName="Chapter 20" titleTimeBegin="01:05:24:00" />
				<Chapter displayName="Chapter 21" titleTimeBegin="01:08:56:10" />
				<Chapter displayName="Chapter 22" titleTimeBegin="01:11:21:00" />
				<Chapter displayName="Chapter 23" titleTimeBegin="01:15:56:30" />
				<Chapter displayName="Chapter 24" titleTimeBegin="01:20:05:10" />
				<Chapter displayName="Chapter 25" titleTimeBegin="01:25:04:00" />
				<Chapter displayName="Chapter 26" titleTimeBegin="01:28:46:20" />
				<Chapter displayName="Chapter 27" titleTimeBegin="01:31:31:40" />
				<Chapter displayName="Chapter 28" titleTimeBegin="01:35:08:50" />
				<Chapter displayName="Chapter 29" titleTimeBegin="01:40:18:00" />
			</ChapterList>
			<TrackNavigationList>
				<AudioTrack track="1" langcode="en:01"/>
				<AudioTrack track="2" langcode="fr:01"/>
				<AudioTrack track="3" langcode="es:01"/>
				<AudioTrack track="4" langcode="en:03"/>
				<SubtitleTrack track="1" langcode="en:01"/>
				<SubtitleTrack track="2" langcode="en:01" description="English SDH"/>
				<SubtitleTrack track="3" langcode="fr:01"/>
				<SubtitleTrack track="4" langcode="es:01"/>
			</TrackNavigationList>
		</Title>
				
	</TitleSet>

</Playlist>
